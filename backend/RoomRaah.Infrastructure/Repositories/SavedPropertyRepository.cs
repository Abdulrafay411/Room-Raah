using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class SavedPropertyRepository : ISavedPropertyRepository
{
    private readonly RoomRaahDbContext _context;

    public SavedPropertyRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Reads past the query filter on purpose. The filter hides a row whose listing
    /// has been deactivated, and rule 87 says a shortlist must always be clearable —
    /// a row that could not be found could not be deleted either.
    /// </summary>
    public async Task<SavedProperty?> GetAsync(int userId, int propertyId)
    {
        return await _context.SavedProperties
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PropertyId == propertyId);
    }

    public Task CreateAsync(SavedProperty saved)
    {
        _context.SavedProperties.Add(saved);
        return Task.CompletedTask;
    }

    public void Remove(SavedProperty saved)
    {
        _context.SavedProperties.Remove(saved);
    }

    public async Task<List<SavedProperty>> GetPageAsync(int userId, int page, int pageSize)
    {
        return await Visible(userId)
            .Include(s => s.Property).ThenInclude(p => p.Area).ThenInclude(a => a.City)
            .Include(s => s.Property).ThenInclude(p => p.Photos)
            .OrderByDescending(s => s.SavedAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountAsync(int userId)
    {
        return Visible(userId).CountAsync();
    }

    /// <summary>
    /// Rule 87 in one place. The page and the count are taken from the same set, or
    /// the last page of a shortlist is empty and nobody can explain why.
    /// </summary>
    private IQueryable<SavedProperty> Visible(int userId)
    {
        return _context.SavedProperties
            .Where(s => s.UserId == userId && s.Property.Status == PropertyStatus.Published);
    }
}
