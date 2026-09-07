using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

public class LocationProfile : Profile
{
    public LocationProfile()
    {
        CreateMap<City, CityDto>();
        CreateMap<Area, AreaDto>();

        CreateMap<City, LocationSuggestionDto>()
            .ForMember(d => d.Type, opt => opt.MapFrom(_ => LocationSuggestionDto.CityType))
            .ForMember(d => d.City, opt => opt.Ignore())
            .ForMember(d => d.Kind, opt => opt.Ignore())
            .ForMember(d => d.Latitude, opt => opt.Ignore())
            .ForMember(d => d.Longitude, opt => opt.Ignore());

        CreateMap<Area, LocationSuggestionDto>()
            .ForMember(d => d.Type, opt => opt.MapFrom(_ => LocationSuggestionDto.AreaType))
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.City.Name))
            .ForMember(d => d.Kind, opt => opt.Ignore())
            .ForMember(d => d.Latitude, opt => opt.Ignore())
            .ForMember(d => d.Longitude, opt => opt.Ignore());

        CreateMap<Landmark, LocationSuggestionDto>()
            .ForMember(d => d.Type, opt => opt.MapFrom(_ => LocationSuggestionDto.LandmarkType))
            .ForMember(d => d.City, opt => opt.MapFrom(s => s.City.Name))
            .ForMember(d => d.Kind, opt => opt.MapFrom(s => s.Kind.ToString()));
    }
}
