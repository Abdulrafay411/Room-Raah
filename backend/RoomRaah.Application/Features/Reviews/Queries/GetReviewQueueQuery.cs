using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reviews.Queries;

public class GetReviewQueueQuery : IRequest<PagedResult<AdminReviewDto>>
{
    /// <summary>Optional. Omitted, the queue is every review in the product.</summary>
    public string? State { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
