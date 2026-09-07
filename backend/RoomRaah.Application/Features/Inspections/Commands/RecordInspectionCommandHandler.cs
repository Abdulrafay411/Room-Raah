using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Common;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Inspections.Commands;

public class RecordInspectionCommandHandler
    : IRequestHandler<RecordInspectionCommand, PropertyInspectionDto>
{
    private readonly IUserRepository _users;
    private readonly IPropertyRepository _properties;
    private readonly IInspectionRepository _inspections;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RecordInspectionCommandHandler> _logger;

    public RecordInspectionCommandHandler(
        IUserRepository users,
        IPropertyRepository properties,
        IInspectionRepository inspections,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RecordInspectionCommandHandler> logger)
    {
        _users = users;
        _properties = properties;
        _inspections = inspections;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PropertyInspectionDto> Handle(
        RecordInspectionCommand request, CancellationToken cancellationToken)
    {
        // Rule 109. Not every admin makes visits — the seeder has one of each — and
        // this is asked before anything is read about the listing.
        var admin = await _users.GetByIdAsync(request.AdminId)
            ?? throw new NotFoundException(InspectionMessages.UserNotFound(request.AdminId));

        BadgeRules.RequireInspector(admin);

        var property = await _properties.GetByIdAsync(request.PropertyId)
            ?? throw new NotFoundException(PropertyMessages.PropertyNotFound(request.PropertyId));

        if (property.Status != PropertyStatus.Published)
        {
            throw new BusinessRuleException(InspectionMessages.OnlyPublishedCanBeInspected);
        }

        var inspection = new PropertyInspection
        {
            PropertyId = property.Id,
            InspectedAt = request.InspectedAt!.Value,
            InspectorAdminId = admin.Id,
            Result = EnumName.Parse<InspectionResult>(request.Result)!.Value,
            Notes = request.Notes?.Trim(),
            FeeAmount = request.FeeAmount,
            FeeCollectedAt = request.FeeCollectedAt
        };

        await _inspections.CreateAsync(inspection);

        // Rule 109. The date users are shown on the listing, and the date rule 114's
        // worklist counts from — which is the only reason the worklist needs no
        // schedule to read.
        property.LastInspectionAt = inspection.InspectedAt;

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Admin {AdminId} recorded a {Result} inspection of property {PropertyId}",
            admin.Id, inspection.Result, property.Id);

        var dto = _mapper.Map<PropertyInspectionDto>(inspection);

        // Mapped from the admin already in hand rather than reloading the graph for
        // one name.
        dto.InspectorDisplayName = admin.FullName;

        return dto;
    }
}
