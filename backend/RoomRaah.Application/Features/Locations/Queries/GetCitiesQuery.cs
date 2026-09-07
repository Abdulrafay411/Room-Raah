using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Locations.Queries;

public class GetCitiesQuery : IRequest<List<CityDto>>
{
}
