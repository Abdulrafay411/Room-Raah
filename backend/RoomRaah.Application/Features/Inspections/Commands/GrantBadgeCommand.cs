using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Inspections.Commands;

/// <summary>
/// Rule 110. §5 listed only the removal, which left rule 50 requiring a reason and an
/// audit row for granting a badge that nothing in the product granted. Recording an
/// inspection and granting the badge are two acts: an inspection is a finding, and the
/// badge is what an admin does about it.
/// </summary>
public class GrantBadgeCommand : IRequest<AdminPropertyDto>, IBadgeChange
{
    public int PropertyId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int AdminId { get; set; }

    /// <summary>Required. Rule 112.</summary>
    public string? Reason { get; set; }
}

public class GrantBadgeCommandHandler : BadgeChangeHandler<GrantBadgeCommand>
{
    public GrantBadgeCommandHandler(
        IUserRepository users,
        IPropertyRepository properties,
        IInspectionRepository inspections,
        IBadgeEventRepository events,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GrantBadgeCommandHandler> logger)
        : base(users, properties, inspections, events, cache, unitOfWork, mapper, logger)
    {
    }

    protected override BadgeAction Action => BadgeAction.Granted;

    /// <summary>Rule 110.</summary>
    protected override bool RequiresInspector => true;

    protected override void Guard(Property property, InspectionResult? latest) =>
        BadgeRules.RequireGrantable(property, latest);
}
