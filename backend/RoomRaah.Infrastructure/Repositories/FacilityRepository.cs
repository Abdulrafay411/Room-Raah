using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class FacilityRepository : IFacilityRepository
{
    private readonly RoomRaahDbContext _context;

    public FacilityRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public async Task<List<Facility>> GetAllAsync()
    {
        return await _context.Facilities
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<List<Facility>> GetByIdsAsync(IReadOnlyCollection<int> ids)
    {
        return await _context.Facilities
            .AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync();
    }
}
