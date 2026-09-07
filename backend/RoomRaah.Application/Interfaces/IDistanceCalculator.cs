using System.Linq.Expressions;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// Straight-line distance from a landmark to a listing.
///
/// Both members hand back expressions rather than numbers, because rule 31 says the
/// distance is computed in the database: a query has to be able to put it in its
/// WHERE, its ORDER BY and its paging. Compiling <see cref="KilometresFrom"/> measures
/// one listing in memory, and that is how the number printed on a card is provably
/// the same formula the sort used rather than a second implementation beside it.
/// </summary>
public interface IDistanceCalculator
{
    /// <summary>How far the listing is from the given point, in kilometres.</summary>
    Expression<Func<Property, double>> KilometresFrom(double latitude, double longitude);

    /// <summary>Whether the listing is within the given distance of the point.</summary>
    Expression<Func<Property, bool>> Within(double latitude, double longitude, double kilometres);
}
