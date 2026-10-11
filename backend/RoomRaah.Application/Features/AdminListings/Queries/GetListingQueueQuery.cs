using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetListingQueueQuery : IRequest<PagedResult<AdminPropertySummaryDto>>
{
    /// <summary>
    /// Optional. Omitted, the queue is every listing in the product, which is what
    /// the admin panel opens on before a tab is chosen.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Optional. Rule 135: matches the listing's title, its city or its owner's display
    /// name, so an admin can find a listing without knowing its number.
    /// </summary>
    public string? Q { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
