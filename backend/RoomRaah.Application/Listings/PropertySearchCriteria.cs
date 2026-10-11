using System.Globalization;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// A search after its ids have been resolved: the landmark has become a pair of
/// coordinates, and everything the query needs is here and nothing else is.
/// </summary>
public sealed class PropertySearchCriteria
{
    public int? CityId { get; init; }

    public int? AreaId { get; init; }

    public double? LandmarkLatitude { get; init; }

    public double? LandmarkLongitude { get; init; }

    public double? MaxDistanceKm { get; init; }

    public decimal? MinRent { get; init; }

    public decimal? MaxRent { get; init; }

    public RoomType? RoomType { get; init; }

    public GenderPolicy? GenderPolicy { get; init; }

    /// <summary>Rule 76: a listing matches only if it offers every one of these.</summary>
    public IReadOnlyList<int> FacilityIds { get; init; } = [];

    public bool AvailableOnly { get; init; }

    /// <summary>Rule 134: the map box. Either all four edges are set or none is.</summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    public PropertySort Sort { get; init; } = PropertySort.Recent;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 12;

    /// <summary>
    /// Rule 83: the whole search written out in a fixed order, as the tail of its
    /// cache key. Written rather than hashed because a key is logged, and nothing in
    /// a search filter is a secret. The facility ids are sorted and the fields are
    /// always in this order, so two requests that mean the same thing are one entry.
    ///
    /// Every filter above has to appear here. A filter that is used by the query and
    /// missing from the key serves one search's results to another, which is the
    /// worst failure this class can have and the quietest — so a test walks these
    /// properties by reflection and fails if one of them is not named below.
    /// </summary>
    public string Describe()
    {
        var parts = new[]
        {
            $"{nameof(CityId)}={Number(CityId)}",
            $"{nameof(AreaId)}={Number(AreaId)}",
            $"{nameof(LandmarkLatitude)}={Number(LandmarkLatitude)}",
            $"{nameof(LandmarkLongitude)}={Number(LandmarkLongitude)}",
            $"{nameof(MaxDistanceKm)}={Number(MaxDistanceKm)}",
            $"{nameof(MinRent)}={Number(MinRent)}",
            $"{nameof(MaxRent)}={Number(MaxRent)}",
            $"{nameof(RoomType)}={RoomType}",
            $"{nameof(GenderPolicy)}={GenderPolicy}",
            $"{nameof(FacilityIds)}={IdList.Format(FacilityIds.Distinct().Order())}",
            $"{nameof(AvailableOnly)}={AvailableOnly}",
            $"{nameof(MinLatitude)}={Number(MinLatitude)}",
            $"{nameof(MaxLatitude)}={Number(MaxLatitude)}",
            $"{nameof(MinLongitude)}={Number(MinLongitude)}",
            $"{nameof(MaxLongitude)}={Number(MaxLongitude)}",
            $"{nameof(Sort)}={Sort}",
            $"{nameof(Page)}={Number<int>(Page)}",
            $"{nameof(PageSize)}={Number<int>(PageSize)}"
        };

        return string.Join('|', parts);
    }

    private static string Number<T>(T? value) where T : struct, IFormattable =>
        value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
}
