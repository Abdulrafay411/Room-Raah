using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Inspections.Queries;

/// <summary>
/// Rule 114. §Business Model asks for a list the admin can work from, and a WHERE
/// clause answers that exactly as well as a background job would — without a sweep
/// that has to have run for the answer to be right.
/// </summary>
public class GetDueForInspectionQuery : IRequest<PagedResult<DueForInspectionDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    /// <summary>The moment the worklist is built. On the query so a test can move it.</summary>
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}
