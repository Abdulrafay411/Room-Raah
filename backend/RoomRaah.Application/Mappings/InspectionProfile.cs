using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

/// <summary>
/// Inspections, badge events and the admin's view of an account.
///
/// Two things are deliberately not mapped and both matter. The owner's inspection
/// state has no field for a future visit, because no such field exists on the entity
/// — rule 115 is enforced by the shape of the data rather than by remembering to
/// filter it. And <see cref="AdminUserSummaryDto"/> is a separate type from
/// <see cref="AdminUserDto"/> rather than a filtered one, so the list cannot gain a
/// CNIC by inheriting one.
/// </summary>
public class InspectionProfile : Profile
{
    public InspectionProfile()
    {
        CreateMap<PropertyInspection, PropertyInspectionDto>()
            .ForMember(d => d.InspectorDisplayName,
                opt => opt.MapFrom(s => s.InspectorAdmin.FullName));

        CreateMap<BadgeEvent, BadgeEventDto>()
            .ForMember(d => d.ActedByDisplayName,
                opt => opt.MapFrom(s => s.ActedByAdmin.FullName));

        CreateMap<Property, OwnerInspectionStateDto>()
            .ForMember(d => d.PropertyId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.InspectionRequested,
                opt => opt.MapFrom(s => s.InspectionRequestedAt != null));

        // DaysOverdue is arithmetic against the moment the query ran, so the handler
        // fills it — a mapping would have to invent a clock to know.
        CreateMap<Property, DueForInspectionDto>()
            .ForMember(d => d.PropertyId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.OwnerDisplayName, opt => opt.MapFrom(s => s.Owner.FullName))
            .ForMember(d => d.DaysOverdue, opt => opt.Ignore());

        CreateMap<Property, RequestedInspectionDto>()
            .ForMember(d => d.PropertyId, opt => opt.MapFrom(s => s.Id))
            .ForMember(d => d.AreaName, opt => opt.MapFrom(s => s.Area.Name))
            .ForMember(d => d.CityName, opt => opt.MapFrom(s => s.Area.City.Name))
            .ForMember(d => d.OwnerDisplayName, opt => opt.MapFrom(s => s.Owner.FullName))
            .ForMember(d => d.InspectionRequestedAt,
                opt => opt.MapFrom(s => s.InspectionRequestedAt ?? default(DateTime)));

        CreateMap<AppUser, AdminUserSummaryDto>();

        // Rule 116. The three profile-shaped fields are conditional on the role having
        // that profile at all, which AutoMapper reads straight off the null.
        CreateMap<AppUser, AdminUserDto>()
            .IncludeBase<AppUser, AdminUserSummaryDto>()
            .ForMember(d => d.CnicNumber,
                opt => opt.MapFrom(s => s.OwnerProfile == null ? null : s.OwnerProfile.CnicNumber))
            .ForMember(d => d.IdentityStatus,
                opt => opt.MapFrom(s => s.OwnerProfile == null
                    ? null
                    : s.OwnerProfile.IdentityStatus.ToString()))
            .ForMember(d => d.IdentityCheckedAt,
                opt => opt.MapFrom(s => s.OwnerProfile == null
                    ? null
                    : s.OwnerProfile.IdentityCheckedAt))
            .ForMember(d => d.CanInspect,
                opt => opt.MapFrom(s => s.AdminProfile == null
                    ? (bool?)null
                    : s.AdminProfile.CanInspect));
    }
}
