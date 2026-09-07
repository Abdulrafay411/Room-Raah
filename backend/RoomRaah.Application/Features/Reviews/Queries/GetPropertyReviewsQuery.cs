using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reviews.Queries;

public class GetPropertyReviewsQuery : IRequest<PropertyReviewsDto>
{
    public int PropertyId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
