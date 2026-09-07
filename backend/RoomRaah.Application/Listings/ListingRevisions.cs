using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Turning a listing into a revision and a revision back into a listing. Both
/// directions live here because they have to agree field for field, and a copy
/// written twice is a copy that drifts.
/// </summary>
public static class ListingRevisions
{
    /// <summary>
    /// A revision opens holding the listing exactly as it stands. Whatever the owner
    /// is changing is then written over the top, which means every field an admin
    /// reads is populated even when only one of them moved.
    /// </summary>
    public static PropertyRevision Snapshot(Property property, RevisionKind kind) => new()
    {
        PropertyId = property.Id,
        Kind = kind,
        ReviewStatus = RevisionStatus.Pending,
        AreaId = property.AreaId,
        Title = property.Title,
        Description = property.Description,
        AddressLine = property.AddressLine,
        Latitude = property.Latitude,
        Longitude = property.Longitude,
        RoomType = property.RoomType,
        GenderPolicy = property.GenderPolicy,
        TotalBeds = property.TotalBeds,
        AvailableBeds = property.AvailableBeds,
        MonthlyRent = property.MonthlyRent,
        SecurityDeposit = property.SecurityDeposit,
        UtilitiesCharge = property.UtilitiesCharge,
        MessCharge = property.MessCharge,
        HouseRules = property.HouseRules,
        FacilityIds = IdList.Format(property.Facilities.Select(f => f.FacilityId).OrderBy(id => id)),
        SubmittedAt = DateTime.UtcNow
    };

    /// <summary>Writes a proposal onto a revision.</summary>
    public static void Propose(PropertyRevision revision, ProposedListing proposed)
    {
        revision.AreaId = proposed.AreaId;
        revision.Title = proposed.Title;
        revision.Description = proposed.Description;
        revision.AddressLine = proposed.AddressLine;
        revision.Latitude = proposed.Latitude;
        revision.Longitude = proposed.Longitude;
        revision.RoomType = proposed.RoomType;
        revision.GenderPolicy = proposed.GenderPolicy;
        revision.TotalBeds = proposed.TotalBeds;
        revision.AvailableBeds = proposed.AvailableBeds;
        revision.MonthlyRent = proposed.MonthlyRent;
        revision.SecurityDeposit = proposed.SecurityDeposit;
        revision.UtilitiesCharge = proposed.UtilitiesCharge;
        revision.MessCharge = proposed.MessCharge;
        revision.HouseRules = proposed.HouseRules;
        revision.FacilityIds = IdList.Format(proposed.FacilityIds.OrderBy(id => id));
        revision.SubmittedAt = DateTime.UtcNow;
    }

    /// <summary>Writes a proposal onto the live listing. Facilities are the caller's job.</summary>
    public static void ApplyToListing(Property property, ProposedListing proposed)
    {
        property.AreaId = proposed.AreaId;
        property.Title = proposed.Title;
        property.Description = proposed.Description;
        property.AddressLine = proposed.AddressLine;
        property.Latitude = proposed.Latitude;
        property.Longitude = proposed.Longitude;
        property.RoomType = proposed.RoomType;
        property.GenderPolicy = proposed.GenderPolicy;
        property.TotalBeds = proposed.TotalBeds;
        property.AvailableBeds = proposed.AvailableBeds;
        property.MonthlyRent = proposed.MonthlyRent;
        property.SecurityDeposit = proposed.SecurityDeposit;
        property.UtilitiesCharge = proposed.UtilitiesCharge;
        property.MessCharge = proposed.MessCharge;
        property.HouseRules = proposed.HouseRules;
    }

    /// <summary>
    /// Rule 21. A move of the bed count on its own is availability and belongs in the
    /// fast queue; anything else is content. Read off the change rather than off the
    /// endpoint, so it holds whichever route the owner's client used. Null means
    /// nothing moved at all.
    /// </summary>
    public static RevisionKind? KindOf(ProposedListing live, ProposedListing proposed)
    {
        var changed = live.DifferencesFrom(proposed);

        if (changed.Count == 0)
        {
            return null;
        }

        return changed.Count == 1 && changed[0] == nameof(ProposedListing.AvailableBeds)
            ? RevisionKind.Availability
            : RevisionKind.Content;
    }
}
