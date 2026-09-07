using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IInspectionRepository
{
    Task CreateAsync(PropertyInspection inspection);

    /// <summary>
    /// Rule 110's question, and only its answer. The *latest* verdict rather than
    /// "was there ever a passed one" — a listing that passed in March and failed in
    /// April has a passed inspection in its history and no business carrying a badge.
    /// </summary>
    Task<InspectionResult?> LatestResultAsync(int propertyId);

    /// <summary>The visits on one listing, most recent first, for the admin's view of it.</summary>
    Task<List<PropertyInspection>> GetForPropertyAsync(int propertyId);
}

public interface IBadgeEventRepository
{
    Task CreateAsync(BadgeEvent badgeEvent);

    /// <summary>The badge's audit trail for one listing, most recent first.</summary>
    Task<List<BadgeEvent>> GetForPropertyAsync(int propertyId);
}
