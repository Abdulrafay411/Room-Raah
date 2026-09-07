using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

public class FacilityProfile : Profile
{
    public FacilityProfile()
    {
        CreateMap<Facility, FacilityDto>();
    }
}
