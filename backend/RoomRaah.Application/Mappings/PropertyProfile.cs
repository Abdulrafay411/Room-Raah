using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

public class PropertyProfile : Profile
{
    public PropertyProfile()
    {
        CreateMap<PropertyPhoto, PropertyPhotoDto>()
            .ForMember(d => d.Url, opt => opt.MapFrom(s => s.BlobUrl));

        CreateMap<Property, OwnerPropertyDto>()
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityId, opt => opt.MapFrom(s => s.Area.CityId))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.Facilities, opt => opt.MapFrom(s => s.Facilities.Select(f => f.Facility)))
            .ForMember(d => d.Photos, opt => opt.MapFrom(s => s.Photos.OrderBy(p => p.SortOrder)));

        CreateMap<Property, OwnerPropertySummaryDto>()
            .ForMember(d => d.BadgeRemovedAt,
                opt => opt.MapFrom(s => BadgeRules.CurrentRemoval(s) == null
                    ? (DateTime?)null
                    : BadgeRules.CurrentRemoval(s)!.ActedAt))
            .ForMember(d => d.BadgeRemovedReason,
                opt => opt.MapFrom(s => BadgeRules.CurrentRemoval(s) == null
                    ? null
                    : BadgeRules.CurrentRemoval(s)!.Reason))
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.PhotoCount, opt => opt.MapFrom(s => s.Photos.Count))
            .ForMember(d => d.PrimaryThumbnailUrl, opt => opt.MapFrom(s =>
                s.Photos.Where(p => p.IsPrimary)
                    .Select(p => p.ThumbnailUrl)
                    .FirstOrDefault()));
    }
}
