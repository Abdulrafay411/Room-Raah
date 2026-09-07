using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Visits.Queries;

public class GetOwnerVisitsQuery : IRequest<PagedResult<VisitRequestDto>>
{
    /// <summary>Set from the token by the controller, never from the query string.</summary>
    public int OwnerId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
