using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>
/// The landing page. Rule 82: at most eight, badge first, then the most recently
/// confirmed availability. It takes no parameters on purpose — the same list for
/// every visitor is what makes a ten-minute cache lifetime safe.
/// </summary>
public class GetFeaturedPropertiesQuery : IRequest<List<ListingCardDto>>
{
    public const int Count = 8;
}
