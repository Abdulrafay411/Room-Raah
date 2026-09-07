using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

/// <summary>
/// The fast half of the two queues. Confirming that the bed count is still right is
/// the thing owners are asked to do most often, and it is the reason a listing can
/// claim to be fresh, so it has a route of its own that asks for one number.
/// </summary>
public class ConfirmAvailabilityCommandHandler
    : IRequestHandler<ConfirmAvailabilityCommand, OwnerListingUpdateDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ConfirmAvailabilityCommandHandler(
        IPropertyRepository properties,
        IPropertyRevisionRepository revisions,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _revisions = revisions;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<OwnerListingUpdateDto> Handle(
        ConfirmAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        if (property.Status == PropertyStatus.PendingReview)
        {
            throw new BusinessRuleException(ReviewMessages.EditLockedWhileUnderReview);
        }

        var beds = request.AvailableBeds!.Value;

        // The upper bound is the listing's own total, which the validator cannot see
        // because it only has the request. A count above it is still a shape problem
        // rather than a rule about state, so it answers 400 like the rest of the
        // field validation table.
        if (beds > property.TotalBeds)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    "AvailableBeds",
                    $"Available beds cannot exceed the {property.TotalBeds} beds this listing has.")
            ]);
        }

        PropertyRevision? revision = null;

        if (property.Status == PropertyStatus.Published)
        {
            // Rule 61's exception. An unchanged count is not an empty revision, it is
            // the confirmation itself, and approving it is what re-stamps the date
            // the listing shows as its last confirmation.
            if (await _revisions.GetPendingAsync(property.Id, RevisionKind.Availability) is not null)
            {
                throw new BusinessRuleException(ReviewMessages.RevisionAlreadyPending);
            }

            revision = ListingRevisions.Snapshot(property, RevisionKind.Availability);
            revision.AvailableBeds = beds;

            await _revisions.CreateAsync(revision);
            await _unitOfWork.SaveChangesAsync();
        }
        else
        {
            var now = DateTime.UtcNow;

            property.AvailableBeds = beds;
            property.AvailabilityConfirmedAt = now;
            property.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync();
            await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
        }

        var saved = await _properties.GetByIdAsync(property.Id);

        return new OwnerListingUpdateDto
        {
            Listing = _mapper.Map<OwnerPropertyDto>(saved),
            PendingRevision = revision is null ? null : RevisionView.Build(revision, saved!, _mapper)
        };
    }
}
