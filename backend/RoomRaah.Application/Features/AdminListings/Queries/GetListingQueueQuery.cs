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

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
