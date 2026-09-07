using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Mappings;

/// <summary>
/// The public face of a listing. Kept apart from <see cref="PropertyProfile"/> so
/// that the owner and admin shapes and the public ones cannot be edited as one:
/// adding a field to a listing means deciding, here, whether a stranger may read it.
///
/// Every gallery below goes through <see cref="ListingGallery.Public"/> — rule 79 is
/// a single expression in one file rather than a filter repeated at five call sites.
/// </summary>
public class PublicListingProfile : Profile
{
    public PublicListingProfile()
    {
        CreateMap<PropertyPhoto, ListingPhotoDto>()
            .ForMember(d => d.Url, opt => opt.MapFrom(s => s.BlobUrl));

        CreateMap<VerificationCheck, ListingCheckDto>();

        CreateMap<Property, ListingOwnerDto>()
            .ForMember(d => d.DisplayName, opt => opt.MapFrom(s => s.Owner.FullName))
            .ForMember(d => d.IdentityVerified, opt => opt.MapFrom(s =>
                s.VerificationChecks.Any(c =>
                    c.CheckType == VerificationCheckType.OwnerIdentity &&
                    c.Status == VerificationStatus.Checked)))
            .ForMember(d => d.IdentityCheckedAt, opt => opt.MapFrom(s =>
                s.VerificationChecks
                    .Where(c =>
                        c.CheckType == VerificationCheckType.OwnerIdentity &&
                        c.Status == VerificationStatus.Checked)
                    .Select(c => c.EvidenceDate)
                    .FirstOrDefault()));

        CreateMap<Property, ListingCardDto>()
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityId, opt => opt.MapFrom(s => s.Area.CityId))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.PhotoCount, opt => opt.MapFrom(s => ListingGallery.Public(s).Count))
            .ForMember(d => d.PrimaryThumbnailUrl, opt => opt.MapFrom(s =>
                ListingGallery.Public(s)
                    .Where(p => p.IsPrimary)
                    .Select(p => p.ThumbnailUrl)
                    .FirstOrDefault()))
            // Filled in by the handler, which is the only thing that knows whether a
            // landmark was named. Left alone here so a mapping cannot quietly turn an
            // absent distance into zero.
            .ForMember(d => d.DistanceKm, opt => opt.Ignore());

        CreateMap<Property, ListingComparisonDto>()
            .IncludeBase<Property, ListingCardDto>()
            .ForMember(d => d.Facilities, opt => opt.MapFrom(s => s.Facilities.Select(f => f.Facility)));

        CreateMap<Property, ListingDetailDto>()
            .IncludeBase<Property, ListingCardDto>()
            .ForMember(d => d.Facilities, opt => opt.MapFrom(s => s.Facilities.Select(f => f.Facility)))
            .ForMember(d => d.Photos, opt => opt.MapFrom(s => ListingGallery.Public(s)))
            .ForMember(d => d.Owner, opt => opt.MapFrom(s => s))
            .ForMember(d => d.Checks, opt => opt.MapFrom(s =>
                s.VerificationChecks.OrderBy(c => c.CheckType)));
    }
}
