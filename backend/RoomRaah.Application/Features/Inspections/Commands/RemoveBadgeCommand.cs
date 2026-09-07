using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Inspections.Commands;

/// <summary>Rule 111.</summary>
public class RemoveBadgeCommand : IRequest<AdminPropertyDto>, IBadgeChange
{
    public int PropertyId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int AdminId { get; set; }

    /// <summary>Required. Rule 112: a badge that quietly disappears is worse than no badge.</summary>
    public string? Reason { get; set; }
}

public class RemoveBadgeCommandHandler : BadgeChangeHandler<RemoveBadgeCommand>
{
    public RemoveBadgeCommandHandler(
        IUserRepository users,
        IPropertyRepository properties,
        IInspectionRepository inspections,
        IBadgeEventRepository events,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RemoveBadgeCommandHandler> logger)
        : base(users, properties, inspections, events, cache, unitOfWork, mapper, logger)
    {
    }

    protected override BadgeAction Action => BadgeAction.Removed;

    /// <summary>
    /// Rule 111: any admin. A badge coming off is a claim being withdrawn, and it has
    /// to be possible the moment a report is upheld or a re-inspection fails —
    /// requiring the one person who makes visits would leave the badge standing in
    /// exactly the case it should not.
    /// </summary>
    protected override bool RequiresInspector => false;

    protected override void Guard(Property property, InspectionResult? latest) =>
        BadgeRules.RequireRemovable(property);
}
