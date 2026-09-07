using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class InspectionRepository : IInspectionRepository
{
    private readonly RoomRaahDbContext _context;

    public InspectionRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(PropertyInspection inspection)
    {
        _context.PropertyInspections.Add(inspection);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rule 110. The latest verdict, not "was there ever a passed one": a listing that
    /// passed in March and failed in April has a passed inspection in its history and
    /// no business carrying a badge. Broken by the id, so two visits recorded with the
    /// same timestamp still have a definite last one.
    /// </summary>
    public async Task<InspectionResult?> LatestResultAsync(int propertyId)
    {
        return await _context.PropertyInspections
            .Where(i => i.PropertyId == propertyId)
            .OrderByDescending(i => i.InspectedAt)
            .ThenByDescending(i => i.Id)
            .Select(i => (InspectionResult?)i.Result)
            .FirstOrDefaultAsync();
    }

    public Task<List<PropertyInspection>> GetForPropertyAsync(int propertyId)
    {
        return _context.PropertyInspections
            .AsNoTracking()
            .Include(i => i.InspectorAdmin)
            .Where(i => i.PropertyId == propertyId)
            .OrderByDescending(i => i.InspectedAt)
            .ThenByDescending(i => i.Id)
            .ToListAsync();
    }
}

public class BadgeEventRepository : IBadgeEventRepository
{
    private readonly RoomRaahDbContext _context;

    public BadgeEventRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(BadgeEvent badgeEvent)
    {
        _context.BadgeEvents.Add(badgeEvent);
        return Task.CompletedTask;
    }

    public Task<List<BadgeEvent>> GetForPropertyAsync(int propertyId)
    {
        return _context.BadgeEvents
            .AsNoTracking()
            .Include(e => e.ActedByAdmin)
            .Where(e => e.PropertyId == propertyId)
            .OrderByDescending(e => e.ActedAt)
            .ThenByDescending(e => e.Id)
            .ToListAsync();
    }
}
