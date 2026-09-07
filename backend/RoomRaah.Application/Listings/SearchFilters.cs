using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Turning a search request into the pieces the query and the cards need: the ids
/// checked (rule 85), the landmark resolved to a pair of coordinates, and one way
/// of measuring a distance for display (rule 80).
///
/// Static and repository-fed rather than an injected service, because none of it
/// holds state and every one of these steps is worth exercising on its own.
/// </summary>
public static class SearchFilters
{
    /// <summary>
    /// The landmark distance is measured from, or null when none was named. A
    /// landmark that names nothing is 422: the id came from a list this API served.
    /// </summary>
    public static async Task<Landmark?> ResolveLandmarkAsync(
        ILocationRepository locations, int? landmarkId)
    {
        if (landmarkId is not { } id)
        {
            return null;
        }

        return await locations.GetLandmarkAsync(id)
            ?? throw new BusinessRuleException(SearchMessages.UnknownLandmark(id));
    }

    public static async Task RequirePlacesAsync(
        ILocationRepository locations, int? cityId, int? areaId)
    {
        if (cityId is { } city && !await locations.CityExistsAsync(city))
        {
            throw new BusinessRuleException(SearchMessages.UnknownCity(city));
        }

        if (areaId is { } area && !await locations.AreaExistsAsync(area))
        {
            throw new BusinessRuleException(SearchMessages.UnknownArea(area));
        }
    }

    /// <summary>
    /// Names every facility id that resolves to nothing rather than the first one,
    /// so a filter panel that has fallen out of date is fixed in one round trip.
    /// </summary>
    public static async Task RequireFacilitiesAsync(
        IFacilityRepository facilities, IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var found = await facilities.GetByIdsAsync(ids);
        var missing = ids.Distinct().Except(found.Select(f => f.Id)).Order().ToList();

        if (missing.Count > 0)
        {
            throw new BusinessRuleException(SearchMessages.UnknownFacilities(missing));
        }
    }

    /// <summary>
    /// Rule 80. Compiles the very expression the query filtered and sorted with, so
    /// the number on the card cannot be a second implementation that drifts from it.
    /// Returns a measure of null when no landmark was named — an absent distance,
    /// which is not the same thing as a distance of zero.
    /// </summary>
    public static Func<Property, double?> Measure(IDistanceCalculator calculator, Landmark? landmark)
    {
        if (landmark is null)
        {
            return _ => null;
        }

        var kilometres = calculator
            .KilometresFrom(landmark.Latitude, landmark.Longitude)
            .Compile();

        return property => Math.Round(kilometres(property), 1);
    }
}
