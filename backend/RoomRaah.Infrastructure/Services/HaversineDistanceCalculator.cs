using System.Linq.Expressions;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Services;

/// <summary>
/// The haversine formula, written so that SQL Server evaluates it: the provider maps
/// SIN, COS, ASIN, SQRT and POWER, so a distance sorts and pages like any other
/// column and the whole search stays one query.
///
/// Haversine rather than the spherical law of cosines, which is shorter to write and
/// loses its precision over exactly the distances this product deals in — a few
/// hundred metres between two hostels on the same road.
///
/// The radians conversion is a local constant, so the compiler folds it into the
/// expression tree. Reading <c>Math.PI</c> inside the tree instead would leave a
/// static field access for EF Core's evaluatable-expression filter to judge, and a
/// judgement that goes the other way pulls the whole formula into memory without
/// saying so.
/// </summary>
public class HaversineDistanceCalculator : IDistanceCalculator
{
    /// <summary>The mean earth radius, in kilometres.</summary>
    private const double EarthRadiusKm = 6371.0088;

    private const double DegreesToRadians = Math.PI / 180d;

    public Expression<Func<Property, double>> KilometresFrom(double latitude, double longitude)
    {
        var lat = latitude * DegreesToRadians;
        var lng = longitude * DegreesToRadians;
        var cosLat = Math.Cos(lat);

        return property => 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(
            Math.Pow(Math.Sin((property.Latitude * DegreesToRadians - lat) / 2), 2) +
            cosLat * Math.Cos(property.Latitude * DegreesToRadians) *
            Math.Pow(Math.Sin((property.Longitude * DegreesToRadians - lng) / 2), 2)));
    }

    /// <summary>
    /// Built from <see cref="KilometresFrom"/> rather than written out a second time.
    /// A filter and a sort that disagreed by a rounding error would drop listings out
    /// of a page for no reason a reader could see.
    /// </summary>
    public Expression<Func<Property, bool>> Within(double latitude, double longitude, double kilometres)
    {
        var distance = KilometresFrom(latitude, longitude);

        return Expression.Lambda<Func<Property, bool>>(
            Expression.LessThanOrEqual(distance.Body, Expression.Constant(kilometres)),
            distance.Parameters);
    }
}
