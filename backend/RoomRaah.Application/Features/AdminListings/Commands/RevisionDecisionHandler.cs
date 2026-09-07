using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminListings.Commands;

/// <summary>The two things an admin can decide about a revision.</summary>
public interface IRevisionDecision
{
    int RevisionId { get; }
    int AdminId { get; }
    string? AdminNote { get; }
}

/// <summary>
/// Approving and rejecting a revision differ only in what they do to the listing.
/// Everything around that — find it, refuse it unless it is still pending, stamp the
/// decision, delete the files of whichever photos lost, drop the cache, email the
/// owner — is shared, and the order of those steps is the part that matters.
/// </summary>
public abstract class RevisionDecisionHandler<TCommand> : IRequestHandler<TCommand, RevisionDetailDto>
    where TCommand : IRequest<RevisionDetailDto>, IRevisionDecision
{
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IPropertyPhotoRepository _photos;
    private readonly IPhotoStorageService _storage;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    protected RevisionDecisionHandler(
        IPropertyRevisionRepository revisions,
        IPropertyPhotoRepository photos,
        IPhotoStorageService storage,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger logger)
    {
        _revisions = revisions;
        _photos = photos;
        _storage = storage;
        _cache = cache;
        _email = email;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    protected abstract RevisionStatus Outcome { get; }

    protected abstract bool NoteRequired { get; }

    protected abstract string DecisionText { get; }

    /// <summary>Changes the listing and returns the photos whose files are now unwanted.</summary>
    protected abstract List<PropertyPhoto> Settle(Property property, PropertyRevision revision, DateTime now);

    public async Task<RevisionDetailDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var revision = await _revisions.GetByIdAsync(request.RevisionId)
            ?? throw new NotFoundException(ReviewMessages.RevisionNotFound(request.RevisionId));

        if (revision.ReviewStatus != RevisionStatus.Pending)
        {
            throw new BusinessRuleException(ReviewMessages.RevisionNotPending);
        }

        var note = NoteRequired
            ? ListingReview.RequireNote(request.AdminNote)
            : request.AdminNote?.Trim();

        var property = revision.Property;
        var now = DateTime.UtcNow;

        var losers = Settle(property, revision, now);

        foreach (var photo in losers)
        {
            await _photos.DeleteAsync(photo);
            property.Photos.Remove(photo);
        }

        // Only once the losing rows are out of the collection, so the order it lays
        // out is the order the listing actually ends up with.
        RevisionApplication.Settle(property, Outcome == RevisionStatus.Approved ? revision : null);

        revision.ReviewStatus = Outcome;
        revision.ReviewedAt = now;
        revision.ReviewedByAdminId = request.AdminId;
        revision.AdminNote = note;
        property.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync();

        // The rows are gone, so the files can go. The other order leaves a listing
        // pointing at a photo that no longer loads, which is the one failure a
        // visitor would actually see.
        foreach (var photo in losers)
        {
            await _storage.DeleteAsync(photo.BlobUrl, photo.ThumbnailUrl);
        }

        await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);

        _logger.LogInformation(
            "Admin {AdminId} {Decision} revision {RevisionId} for property {PropertyId}",
            request.AdminId, Outcome, revision.Id, property.Id);

        await _email.SendRevisionDecisionAsync(
            property.Owner, property.Title, revision.Kind, DecisionText, note);

        return RevisionView.Build(revision, property, _mapper);
    }
}
