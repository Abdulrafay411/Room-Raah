using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// The editable half of a listing, in one shape. An owner's edit arrives as this, a
/// revision stores this, and the live row holds this — so "did anything change" is a
/// comparison of two of them rather than eighteen pairs of fields compared by hand
/// in whichever handler happened to need the answer.
/// </summary>
public sealed record ProposedListing(
    int AreaId,
    string Title,
    string Description,
    string AddressLine,
    double Latitude,
    double Longitude,
    RoomType RoomType,
    GenderPolicy GenderPolicy,
    int TotalBeds,
    int AvailableBeds,
    decimal MonthlyRent,
    decimal SecurityDeposit,
    decimal UtilitiesCharge,
    decimal MessCharge,
    string? HouseRules,
    IReadOnlyList<int> FacilityIds)
{
    /// <summary>The listing exactly as it stands.</summary>
    public static ProposedListing From(Property property) => new(
        property.AreaId,
        property.Title,
        property.Description,
        property.AddressLine,
        property.Latitude,
        property.Longitude,
        property.RoomType,
        property.GenderPolicy,
        property.TotalBeds,
        property.AvailableBeds,
        property.MonthlyRent,
        property.SecurityDeposit,
        property.UtilitiesCharge,
        property.MessCharge,
        property.HouseRules,
        property.Facilities.Select(f => f.FacilityId).OrderBy(id => id).ToList());

    /// <summary>The proposal a revision carries.</summary>
    public static ProposedListing From(PropertyRevision revision) => new(
        revision.AreaId,
        revision.Title,
        revision.Description,
        revision.AddressLine,
        revision.Latitude,
        revision.Longitude,
        revision.RoomType,
        revision.GenderPolicy,
        revision.TotalBeds,
        revision.AvailableBeds,
        revision.MonthlyRent,
        revision.SecurityDeposit,
        revision.UtilitiesCharge,
        revision.MessCharge,
        revision.HouseRules,
        IdList.Parse(revision.FacilityIds).OrderBy(id => id).ToList());

    /// <summary>
    /// The names of the fields that differ, in the order an admin reads them. House
    /// rules are compared as text with null and empty treated alike, because a form
    /// that clears a box sends one of the two and the owner meant the same thing by
    /// either.
    /// </summary>
    public IReadOnlyList<string> DifferencesFrom(ProposedListing other)
    {
        var changed = new List<string>();

        void Check(bool differs, string field)
        {
            if (differs)
            {
                changed.Add(field);
            }
        }

        Check(AreaId != other.AreaId, nameof(AreaId));
        Check(Title != other.Title, nameof(Title));
        Check(Description != other.Description, nameof(Description));
        Check(AddressLine != other.AddressLine, nameof(AddressLine));
        Check(Math.Abs(Latitude - other.Latitude) > 0.0000001, nameof(Latitude));
        Check(Math.Abs(Longitude - other.Longitude) > 0.0000001, nameof(Longitude));
        Check(RoomType != other.RoomType, nameof(RoomType));
        Check(GenderPolicy != other.GenderPolicy, nameof(GenderPolicy));
        Check(TotalBeds != other.TotalBeds, nameof(TotalBeds));
        Check(AvailableBeds != other.AvailableBeds, nameof(AvailableBeds));
        Check(MonthlyRent != other.MonthlyRent, nameof(MonthlyRent));
        Check(SecurityDeposit != other.SecurityDeposit, nameof(SecurityDeposit));
        Check(UtilitiesCharge != other.UtilitiesCharge, nameof(UtilitiesCharge));
        Check(MessCharge != other.MessCharge, nameof(MessCharge));
        Check((HouseRules ?? string.Empty) != (other.HouseRules ?? string.Empty), nameof(HouseRules));
        Check(!FacilityIds.OrderBy(id => id).SequenceEqual(other.FacilityIds.OrderBy(id => id)),
            nameof(FacilityIds));

        return changed;
    }
}
