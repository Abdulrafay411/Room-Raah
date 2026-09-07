using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Locations.Queries;

public class SuggestLocationsQuery : IRequest<List<LocationSuggestionDto>>
{
    /// <summary>A single letter matches most of the table, so it is not a search.</summary>
    public const int MinQueryLength = 2;

    public const int MaxQueryLength = 60;

    /// <summary>A list longer than the screen is no more useful than one that fits.</summary>
    public const int MaxSuggestions = 10;

    /// <summary>
    /// Nullable on purpose. A non-nullable string here is implicitly required by
    /// model binding, which rejects an empty value before the request reaches the
    /// validation pipeline — and answers in a different shape than every other
    /// bad request on this endpoint.
    /// </summary>
    public string? Q { get; set; }
}
