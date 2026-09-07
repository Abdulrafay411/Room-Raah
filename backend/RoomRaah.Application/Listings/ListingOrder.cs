using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Rule 77: what a search is sorted by, and the id every one of those orders ends
/// with. Two listings confirmed in the same second are otherwise free to swap places
/// between one page and the next, and a reader sees one of them twice and never sees
/// the other.
///
/// This lives here rather than in the repository so it can be tested for what it
/// actually is — an expression tree with an id at the end of it. Asserting the same
/// thing by paging through a database only fails when SQL Server happens to reorder
/// the ties, which it is free not to do, and a test that passes when it is free to
/// fail is not a test of this rule.
/// </summary>
public static class ListingOrder
{
    public static IOrderedQueryable<Property> Apply(
        IQueryable<Property> query, PropertySearchCriteria criteria, IDistanceCalculator distance)
    {
        return criteria.Sort switch
        {
            PropertySort.PriceLowToHigh =>
                query.OrderBy(p => p.MonthlyRent).ThenBy(p => p.Id),

            PropertySort.PriceHighToLow =>
                query.OrderByDescending(p => p.MonthlyRent).ThenBy(p => p.Id),

            // Rule 74 keeps the coordinates from being absent here, and the pattern
            // keeps this from being the place that finds out otherwise.
            PropertySort.Distance when criteria is
                { LandmarkLatitude: { } lat, LandmarkLongitude: { } lng } =>
                query.OrderBy(distance.KilometresFrom(lat, lng)).ThenBy(p => p.Id),

            _ => query.OrderByDescending(p => p.AvailabilityConfirmedAt).ThenBy(p => p.Id)
        };
    }
}
