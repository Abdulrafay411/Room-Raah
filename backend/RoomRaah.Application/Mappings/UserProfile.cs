using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<AppUser, UserSummaryDto>()
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()));
    }
}
