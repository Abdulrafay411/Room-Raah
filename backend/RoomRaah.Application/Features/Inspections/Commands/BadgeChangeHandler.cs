using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Inspections.Commands;

/// <summary>What both badge changes carry.</summary>
public interface IBadgeChange
{
    int PropertyId { get; }
    int AdminId { get; }
    string? Reason { get; }
}

/// <summary>
/// Granting and removing differ in who may do it, what has to be true first, and the
/// value they write. Everything else — the reason, the audit row, the cache eviction
/// and the log line — is the same twice, and written twice it would end up being two
/// slightly different things. The same reasoning as <c>ListingDecisionHandler</c>.
/// </summary>
public abstract class BadgeChangeHandler<TCommand> : IRequestHandler<TCommand, AdminPropertyDto>
    where TCommand : IRequest<AdminPropertyDto>, IBadgeChange
{
    private readonly IUserRepository _users;
    private readonly IPropertyRepository _properties;
    private readonly IInspectionRepository _inspections;
    private readonly IBadgeEventRepository _events;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    protected BadgeChangeHandler(
        IUserRepository users,
        IPropertyRepository properties,
        IInspectionRepository inspections,
        IBadgeEventRepository events,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger logger)
    {
        _users = users;
        _properties = properties;
        _inspections = inspections;
        _events = events;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    protected abstract BadgeAction Action { get; }

    /// <summary>
    /// Rule 110 wants an inspector and rule 111 does not. A badge that comes off is a
    /// claim being withdrawn, and any admin should be able to withdraw a claim the
    /// moment a report is upheld or a re-inspection fails — waiting for the one person
    /// who makes visits would leave it standing in exactly the case it should not.
    /// </summary>
    protected abstract bool RequiresInspector { get; }

    protected abstract void Guard(Property property, InspectionResult? latest);

    public async Task<AdminPropertyDto> Handle(TCommand request, CancellationToken cancellationToken)
    {
        var admin = await _users.GetByIdAsync(request.AdminId)
            ?? throw new NotFoundException(InspectionMessages.UserNotFound(request.AdminId));

        if (RequiresInspector)
        {
            BadgeRules.RequireInspector(admin);
        }

        var property = await _properties.GetForReviewAsync(request.PropertyId)
            ?? throw new NotFoundException(PropertyMessages.PropertyNotFound(request.PropertyId));

        // Rule 112. Asked before the state check, so a caller who sends neither is told
        // about the missing field rather than about a rule they were never going to
        // satisfy — a 400 is about the shape of a request and comes first.
        var reason = BadgeRules.RequireReason(request.Reason);

        var latest = await _inspections.LatestResultAsync(property.Id);

        Guard(property, latest);

        property.HasInspectionBadge = Action == BadgeAction.Granted;
        property.UpdatedAt = DateTime.UtcNow;

        await _events.CreateAsync(new BadgeEvent
        {
            PropertyId = property.Id,
            Action = Action,
            Reason = reason,
            ActedByAdminId = admin.Id
        });

        await _unitOfWork.SaveChangesAsync();

        // Rule 113. Rule 82 orders the featured list by the badge and every card shows
        // it, so a badge that came off but is still cached is RoomRaah making a claim
        // about a property it has stopped standing behind — which is the one direction
        // that actually matters.
        await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);

        _logger.LogInformation(
            "Admin {AdminId} {Action} inspection badge on property {PropertyId}: {Reason}",
            admin.Id, Action, property.Id, reason);

        var dto = _mapper.Map<AdminPropertyDto>(property);
        dto.CanPublish = ListingReview.ChecksPass(property);

        return dto;
    }
}
