using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Properties.Commands;

public class CreatePropertyCommandHandler : IRequestHandler<CreatePropertyCommand, OwnerPropertyDto>
{
    private readonly IPropertyRepository _properties;
    private readonly ILocationRepository _locations;
    private readonly IFacilityRepository _facilities;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreatePropertyCommandHandler(
        IPropertyRepository properties,
        ILocationRepository locations,
        IFacilityRepository facilities,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _locations = locations;
        _facilities = facilities;
        _users = users;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<OwnerPropertyDto> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
    {
        // Rule 13, checked against the profile rather than the role claim. Today a
        // token that says Owner always has one, because registration writes both
        // together; the rule is about the profile, so the check is too.
        var user = await _users.GetByIdAsync(request.OwnerId);
        if (user?.OwnerProfile is null)
        {
            throw new UnauthorizedAccessException(PropertyMessages.OwnerProfileRequired);
        }

        if (!await _locations.AreaExistsAsync(request.AreaId))
        {
            throw new BusinessRuleException(PropertyMessages.AreaNotFound);
        }

        var facilityIds = request.FacilityIds.Distinct().ToList();
        if (facilityIds.Count > 0)
        {
            var found = await _facilities.GetByIdsAsync(facilityIds);
            if (found.Count != facilityIds.Count)
            {
                throw new BusinessRuleException(PropertyMessages.FacilityNotFound);
            }
        }

        var now = DateTime.UtcNow;

        var property = new Property
        {
            OwnerId = request.OwnerId,
            AreaId = request.AreaId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AddressLine = request.AddressLine.Trim(),
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            RoomType = Enum.Parse<RoomType>(request.RoomType, ignoreCase: true),
            GenderPolicy = Enum.Parse<GenderPolicy>(request.GenderPolicy, ignoreCase: true),
            TotalBeds = request.TotalBeds!.Value,
            AvailableBeds = request.AvailableBeds!.Value,
            MonthlyRent = request.MonthlyRent!.Value,
            SecurityDeposit = request.SecurityDeposit ?? 0m,
            UtilitiesCharge = request.UtilitiesCharge ?? 0m,
            MessCharge = request.MessCharge ?? 0m,
            HouseRules = string.IsNullOrWhiteSpace(request.HouseRules) ? null : request.HouseRules.Trim(),
            Status = PropertyStatus.Draft,
            AvailabilityConfirmedAt = now,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Facilities = facilityIds
                .Select(id => new PropertyFacility { FacilityId = id })
                .ToList()
        };

        await _properties.CreateAsync(property);
        await _unitOfWork.SaveChangesAsync();

        // Read back rather than mapping the entity that was just built: the response
        // carries the area and city names, and this is the same load GetMyProperty
        // uses, so a create and a fetch cannot answer in different shapes.
        var saved = await _properties.GetByIdAsync(property.Id);

        return _mapper.Map<OwnerPropertyDto>(saved);
    }
}
