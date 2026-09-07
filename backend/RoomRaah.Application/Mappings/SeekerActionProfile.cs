using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

/// <summary>
/// Shortlists, visit requests, reviews and reports. Every shape here names a person
/// by their display name and by nothing else — rule 33 for the owner, rule 93 for
/// the seeker and rule 95 for a review's author are one habit written three times.
/// </summary>
public class SeekerActionProfile : Profile
{
    public SeekerActionProfile()
    {
        // The saved-at stamp belongs to the shortlist row, not to the listing, so the
        // handler fills it. Ignored here for the same reason DistanceKm is: a mapping
        // that quietly produced a default date would be a lie nobody could see.
        CreateMap<Property, SavedListingDto>()
            .IncludeBase<Property, ListingCardDto>()
            .ForMember(d => d.SavedAt, opt => opt.Ignore());

        CreateMap<VisitRequest, VisitRequestDto>()
            .ForMember(d => d.PropertyTitle, opt => opt.MapFrom(s => s.Property.Title))
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Property.Area.Name))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Property.Area.City.Name))
            .ForMember(d => d.SeekerDisplayName, opt => opt.MapFrom(s => s.Seeker.FullName));

        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.AuthorDisplayName, opt => opt.MapFrom(s => s.Author.FullName));

        CreateMap<Review, AdminReviewDto>()
            .IncludeBase<Review, ReviewDto>()
            .ForMember(d => d.PropertyTitle, opt => opt.MapFrom(s => s.Property.Title));

        CreateMap<Review, MyReviewDto>();

        CreateMap<Report, MyReportDto>();

        CreateMap<Report, ReportDto>()
            .IncludeBase<Report, MyReportDto>()
            .ForMember(d => d.PropertyTitle, opt => opt.MapFrom(s => s.Property.Title))
            .ForMember(d => d.ReportedByDisplayName, opt => opt.MapFrom(s => s.ReportedByUser.FullName));
    }
}
