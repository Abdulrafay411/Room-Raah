using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Mappings;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        CreateMap<VerificationCheck, VerificationCheckDto>();

        // The owner as an admin may see them. There is no Email and no PhoneNumber
        // on the destination, so rule 33 holds by the shape of the type rather than
        // by anybody remembering to ignore two members.
        CreateMap<AppUser, ListingOwnerSummaryDto>()
            .ForMember(d => d.IdentityStatus, opt => opt.MapFrom(s =>
                s.OwnerProfile == null
                    ? VerificationStatus.NotChecked.ToString()
                    : s.OwnerProfile.IdentityStatus.ToString()))
            .ForMember(d => d.IdentityCheckedAt, opt => opt.MapFrom(s =>
                s.OwnerProfile == null ? null : s.OwnerProfile.IdentityCheckedAt));

        CreateMap<Property, AdminPropertyDto>()
            .IncludeBase<Property, OwnerPropertyDto>()
            .ForMember(d => d.Owner, opt => opt.MapFrom(s => s.Owner))
            .ForMember(d => d.VerificationChecks, opt => opt.MapFrom(s => s.VerificationChecks))
            .ForMember(d => d.CanPublish, opt => opt.Ignore());

        CreateMap<Property, AdminPropertySummaryDto>()
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.OwnerName, opt => opt.MapFrom(s => s.Owner.FullName))
            .ForMember(d => d.PhotoCount, opt => opt.MapFrom(s => s.Photos.Count))
            .ForMember(d => d.PrimaryThumbnailUrl, opt => opt.MapFrom(s =>
                s.Photos.Where(p => p.IsPrimary)
                    .Select(p => p.ThumbnailUrl)
                    .FirstOrDefault()));
    }
}
