using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>Rules 35 and 81: up to three listings side by side.</summary>
public class ComparePropertiesQuery : IRequest<List<ListingComparisonDto>>
{
    public const int MaxListings = 3;

    /// <summary>Comma-separated listing ids, in the order they should be shown.</summary>
    public string? Ids { get; set; }

    /// <summary>
    /// Optional. Comparing three rooms without knowing which is nearest the
    /// university is comparing on the wrong axis, so the landmark carries over
    /// from the search the shortlist came from.
    /// </summary>
    public int? LandmarkId { get; set; }
}
