using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetListingForReviewQuery : IRequest<AdminPropertyDto>
{
    public int PropertyId { get; set; }
}
