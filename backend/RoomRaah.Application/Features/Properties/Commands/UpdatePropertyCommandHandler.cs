using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UpdatePropertyCommandHandler : IRequestHandler<UpdatePropertyCommand, OwnerListingUpdateDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IPropertyRevisionRepository _revisions;
    private readonly ILocationRepository _locations;
    private readonly IFacilityRepository _facilities;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public UpdatePropertyCommandHandler(
        IPropertyRepository properties,
        IPropertyRevisionRepository revisions,
        ILocationRepository locations,
        IFacilityRepository facilities,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _revisions = revisions;
        _locations = locations;
        _facilities = facilities;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<OwnerListingUpdateDto> Handle(
        UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        // Rule 60. An admin is reading this listing right now, and moving the fields
        // underneath them makes the review they are part-way through meaningless.
        if (property.Status == PropertyStatus.PendingReview)
        {
            throw new BusinessRuleException(ReviewMessages.EditLockedWhileUnderReview);
        }

        var facilityIds = await RequireReferencesExist(request);

        var proposed = new ProposedListing(
            request.AreaId,
            request.Title.Trim(),
            request.Description.Trim(),
            request.AddressLine.Trim(),
            request.Latitude!.Value,
            request.Longitude!.Value,
            Enum.Parse<RoomType>(request.RoomType, ignoreCase: true),
            Enum.Parse<GenderPolicy>(request.GenderPolicy, ignoreCase: true),
            request.TotalBeds!.Value,
            request.AvailableBeds!.Value,
            request.MonthlyRent!.Value,
            request.SecurityDeposit ?? 0m,
            request.UtilitiesCharge ?? 0m,
            request.MessCharge ?? 0m,
            string.IsNullOrWhiteSpace(request.HouseRules) ? null : request.HouseRules.Trim(),
            facilityIds);

        var revision = property.Status == PropertyStatus.Published
            ? await Propose(property, proposed)
            : await WriteDirectly(property, proposed);

        var saved = await _properties.GetByIdAsync(property.Id);

        return new OwnerListingUpdateDto
        {
            Listing = _mapper.Map<OwnerPropertyDto>(saved),
            PendingRevision = revision is null ? null : RevisionView.Build(revision, saved!, _mapper)
        };
    }

    /// <summary>
    /// Rule 19. The live row is not touched: the listing carries on serving the
    /// approved version while the proposal waits for an admin.
    /// </summary>
    private async Task<PropertyRevision?> Propose(Property property, ProposedListing proposed)
    {
        var kind = ListingRevisions.KindOf(ProposedListing.From(property), proposed);

        // Rule 61. An empty revision costs an admin the one thing this queue is short
        // of, which is an admin looking at it.
        if (kind is null)
        {
            throw new BusinessRuleException(ReviewMessages.NothingChanged);
        }

        // Rule 20. Two pending proposals of the same kind would mean an admin
        // approving one while the other still claims to describe the same listing.
        if (await _revisions.GetPendingAsync(property.Id, kind.Value) is not null)
        {
            throw new BusinessRuleException(ReviewMessages.RevisionAlreadyPending);
        }

        var revision = ListingRevisions.Snapshot(property, kind.Value);
        ListingRevisions.Propose(revision, proposed);

        await _revisions.CreateAsync(revision);
        await _unitOfWork.SaveChangesAsync();

        return revision;
    }

    /// <summary>
    /// Rule 60. Nothing public is being protected on a draft, a rejected listing or
    /// one an owner has taken down, so the edit goes straight in.
    /// </summary>
    private async Task<PropertyRevision?> WriteDirectly(Property property, ProposedListing proposed)
    {
        var confirmsAvailability = property.AvailableBeds != proposed.AvailableBeds;

        ListingRevisions.ApplyToListing(property, proposed);

        property.Facilities.Clear();
        foreach (var id in proposed.FacilityIds)
        {
            property.Facilities.Add(new PropertyFacility { PropertyId = property.Id, FacilityId = id });
        }

        var now = DateTime.UtcNow;
        property.UpdatedAt = now;

        if (confirmsAvailability)
        {
            property.AvailabilityConfirmedAt = now;
        }

        await _unitOfWork.SaveChangesAsync();

        // This listing is not published, but it may have been. Anything cached from
        // when it was is now describing values nobody holds any more.
        await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);

        return null;
    }

    /// <summary>Rule 53, the same check the create path makes.</summary>
    private async Task<List<int>> RequireReferencesExist(UpdatePropertyCommand request)
    {
        if (!await _locations.AreaExistsAsync(request.AreaId))
        {
            throw new BusinessRuleException(PropertyMessages.AreaNotFound);
        }

        var facilityIds = request.FacilityIds.Distinct().OrderBy(id => id).ToList();

        if (facilityIds.Count > 0)
        {
            var found = await _facilities.GetByIdsAsync(facilityIds);
            if (found.Count != facilityIds.Count)
            {
                throw new BusinessRuleException(PropertyMessages.FacilityNotFound);
            }
        }

        return facilityIds;
    }
}
