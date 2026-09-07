using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetRevisionQuery : IRequest<RevisionDetailDto>
{
    public int RevisionId { get; set; }
}
