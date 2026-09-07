using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Facilities.Queries;

public class GetFacilitiesQuery : IRequest<List<FacilityDto>>
{
}
