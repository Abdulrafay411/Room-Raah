using System.Globalization;
using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Renders a revision the way an admin reads it: only the fields that moved, each
/// with what the listing says today beside what the owner has asked for. Built here
/// rather than in AutoMapper because a diff is a comparison of two objects and a
/// mapping profile only ever sees one.
/// </summary>
public static class RevisionView
{
    public static RevisionDetailDto Build(PropertyRevision revision, Property property, IMapper mapper)
    {
        var live = ProposedListing.From(property);
        var proposed = ProposedListing.From(revision);

        var current = Values(live);
        var wanted = Values(proposed);

        var changes = live.DifferencesFrom(proposed)
            .Select(field => new FieldChangeDto
            {
                Field = field,
                Current = current[field],
                Proposed = wanted[field]
            })
            .ToList();

        return new RevisionDetailDto
        {
            Id = revision.Id,
            PropertyId = revision.PropertyId,
            PropertyTitle = property.Title,
            Kind = revision.Kind.ToString(),
            ReviewStatus = revision.ReviewStatus.ToString(),
            OwnerName = property.Owner?.FullName ?? string.Empty,
            Changes = changes,
            CurrentPhotos = mapper.Map<List<PropertyPhotoDto>>(ListingGallery.Live(property)),
            PhotosToAdd = mapper.Map<List<PropertyPhotoDto>>(Photos(property, PhotoStatus.PendingAdd)),
            PhotosToRemove = mapper.Map<List<PropertyPhotoDto>>(Photos(property, PhotoStatus.PendingRemove)),
            ProposedPhotoOrder = revision.PhotoOrder is null ? null : IdList.Parse(revision.PhotoOrder),
            SubmittedAt = revision.SubmittedAt,
            ReviewedAt = revision.ReviewedAt,
            AdminNote = revision.AdminNote
        };
    }

    private static List<PropertyPhoto> Photos(Property property, PhotoStatus status) =>
        property.Photos
            .Where(p => p.Status == status)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToList();

    /// <summary>
    /// Every field as text, under the same names <see cref="ProposedListing"/>
    /// reports as changed. Facilities render as ids: the admin panel already holds
    /// the facility reference list and can name them, and a revision does not carry
    /// names of its own to be trusted.
    /// </summary>
    private static Dictionary<string, string?> Values(ProposedListing listing) => new()
    {
        [nameof(ProposedListing.AreaId)] = Text(listing.AreaId),
        [nameof(ProposedListing.Title)] = listing.Title,
        [nameof(ProposedListing.Description)] = listing.Description,
        [nameof(ProposedListing.AddressLine)] = listing.AddressLine,
        [nameof(ProposedListing.Latitude)] = Text(listing.Latitude),
        [nameof(ProposedListing.Longitude)] = Text(listing.Longitude),
        [nameof(ProposedListing.RoomType)] = listing.RoomType.ToString(),
        [nameof(ProposedListing.GenderPolicy)] = listing.GenderPolicy.ToString(),
        [nameof(ProposedListing.TotalBeds)] = Text(listing.TotalBeds),
        [nameof(ProposedListing.AvailableBeds)] = Text(listing.AvailableBeds),
        [nameof(ProposedListing.MonthlyRent)] = Text(listing.MonthlyRent),
        [nameof(ProposedListing.SecurityDeposit)] = Text(listing.SecurityDeposit),
        [nameof(ProposedListing.UtilitiesCharge)] = Text(listing.UtilitiesCharge),
        [nameof(ProposedListing.MessCharge)] = Text(listing.MessCharge),
        [nameof(ProposedListing.HouseRules)] = listing.HouseRules,
        [nameof(ProposedListing.FacilityIds)] = IdList.Format(listing.FacilityIds)
    };

    private static string Text(IFormattable value) =>
        value.ToString(null, CultureInfo.InvariantCulture);

    /// <summary>
    /// Money loses its trailing zeros. A decimal read from the database carries the
    /// column's scale and one parsed from a request body does not, so the same rent
    /// renders as 18000.00 on one side of a diff and 21000 on the other — which
    /// reads as a change of format rather than a change of price.
    /// </summary>
    private static string Text(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
