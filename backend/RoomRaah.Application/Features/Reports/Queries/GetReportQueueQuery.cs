using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reports.Queries;

public class GetReportQueueQuery : IRequest<PagedResult<ReportDto>>
{
    /// <summary>Optional. Omitted, the queue is every report in the product.</summary>
    public string? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
