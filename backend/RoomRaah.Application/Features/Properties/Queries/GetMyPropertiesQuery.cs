using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Queries;

public class GetMyPropertiesQuery : IRequest<PagedResult<OwnerPropertySummaryDto>>
{
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 50;

    /// <summary>
    /// Set from the bearer token by the controller after model binding, which is
    /// what makes an ownerId in the query string harmless: it is overwritten before
    /// the query reaches a handler.
    /// </summary>
    public int OwnerId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;
}
