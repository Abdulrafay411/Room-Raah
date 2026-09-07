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

/// <summary>The three things an admin can decide about a listing.</summary>
public interface IListingDecision
{
    int PropertyId { get; }
    int AdminId { get; }
    string? AdminNote { get; }
}

/// <summary>
/// Approve, reject and request changes differ in the status they leave behind, in
/// whether a note is required and in what they check first. Everything else — find
/// the listing, refuse it unless it is waiting for review, stamp who decided and
/// when, drop the cache, email the owner — is the same three times, and written
/// three times it would end up being three slightly different things.
/// </summary>
public abstract class ListingDecisionHandler<TCommand> : IRequestHandler<TCommand, AdminPropertyDto>
    where TCommand : IRequest<AdminPropertyDto>, IListingDecision
{
    private readonly IPropertyRepository _properties;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    protected ListingDecisionHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger logger)
    {
        _properties = properties;
        _cache = cache;
        _email = email;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>The status the listing is left in.</summary>
    protected abstract PropertyStatus Outcome { get; }

    /// <summary>
    /// The statuses this decision may start from. Rule 62 for three of the four, and
    /// rule 124 for the one that is different: an unpublished listing comes back
    /// through approve, which still has rule 23's four checks standing in front of it.
    /// </summary>
    protected virtual PropertyStatus[] AllowedFrom => [PropertyStatus.PendingReview];

    /// <summary>What to say when the listing is in none of those statuses.</summary>
    protected virtual string NotAllowedMessage => ReviewMessages.NotReviewable;

    /// <summary>Rules 26 and 63: a refusal has to say why.</summary>
    protected abstract bool NoteRequired { get; }

    /// <summary>How the decision reads in the email to the owner.</summary>
    protected abstract string DecisionText { get; }

    /// <summary>Anything this particular decision has to check first.</summary>
    protected virtual void Guard(Property property)
    {
    }

    /// <summary>Anything this particular decision has to write beyond the status.</summary>
    protected virtual void Apply(Property property, DateTime now)
    {
    }

    public async Task<AdminPropertyDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var property = await _properties.GetForReviewAsync(request.PropertyId)
            ?? throw new NotFoundException(PropertyMessages.PropertyNotFound(request.PropertyId));

        ListingReview.RequireStatus(property, AllowedFrom, NotAllowedMessage);
        Guard(property);

        var note = NoteRequired
            ? ListingReview.RequireNote(request.AdminNote)
            : request.AdminNote?.Trim();

        var now = DateTime.UtcNow;

        property.Status = Outcome;
        property.UpdatedAt = now;
        Apply(property, now);

        await _unitOfWork.SaveChangesAsync();

        // Rule 71. An approval is a write, and so is a refusal: whatever was cached
        // about this listing described a state it is no longer in.
        await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);

        _logger.LogInformation(
            "Admin {AdminId} set property {PropertyId} to {Status}",
            request.AdminId, property.Id, Outcome);

        // Rule 70.
        await _email.SendListingDecisionAsync(property.Owner, property.Title, DecisionText, note);

        var dto = _mapper.Map<AdminPropertyDto>(property);
        dto.CanPublish = ListingReview.ChecksPass(property);

        return dto;
    }
}
