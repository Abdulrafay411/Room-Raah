using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Locations.Queries;

public class GetAreasQuery : IRequest<List<AreaDto>>
{
    public int CityId { get; set; }
}
