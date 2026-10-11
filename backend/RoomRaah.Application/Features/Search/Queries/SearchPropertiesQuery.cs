using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>
/// The filters of rule 29, exactly as they arrive in the query string. The enums are
/// strings here and not enum types: a client sending <c>roomType=Twin</c> should be
/// told which values exist, and model binding answers that with an empty message.
/// </summary>
public class SearchPropertiesQuery : IRequest<PagedResult<ListingCardDto>>
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 50;
    public const double MaxDistanceLimitKm = 100;

    public int? CityId { get; set; }

    public int? AreaId { get; set; }

    /// <summary>The university or workplace distance is measured from.</summary>
    public int? LandmarkId { get; set; }

    /// <summary>Rule 74: meaningless, and refused, without a landmark.</summary>
    public double? MaxDistanceKm { get; set; }

    public decimal? MinRent { get; set; }

    public decimal? MaxRent { get; set; }

    /// <summary>Private or Shared.</summary>
    public string? RoomType { get; set; }

    /// <summary>Boys, Girls or Family.</summary>
    public string? GenderPolicy { get; set; }

    /// <summary>Comma-separated facility ids. Rule 76: the listing must offer all of them.</summary>
    public string? FacilityIds { get; set; }

    /// <summary>Rule 78: only listings with a bed free.</summary>
    public bool AvailableOnly { get; set; }

    /// <summary>
    /// Rule 133: the southern edge of the map box. The four edges come together or not
    /// at all.
    /// </summary>
    public double? MinLatitude { get; set; }

    /// <summary>Rule 133: the northern edge of the map box.</summary>
    public double? MaxLatitude { get; set; }

    /// <summary>Rule 133: the western edge of the map box.</summary>
    public double? MinLongitude { get; set; }

    /// <summary>Rule 133: the eastern edge of the map box.</summary>
    public double? MaxLongitude { get; set; }

    /// <summary>Recent, PriceLowToHigh, PriceHighToLow or Distance. Defaults to Recent.</summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;
}
