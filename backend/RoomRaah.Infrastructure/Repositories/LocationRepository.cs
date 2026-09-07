using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class LocationRepository : ILocationRepository
{
    private readonly RoomRaahDbContext _context;

    public LocationRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public async Task<List<City>> GetCitiesAsync()
    {
        return await _context.Cities
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<List<Area>> GetAreasAsync(int cityId)
    {
        return await _context.Areas
            .AsNoTracking()
            .Where(a => a.CityId == cityId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<bool> CityExistsAsync(int cityId)
    {
        return await _context.Cities.AnyAsync(c => c.Id == cityId);
    }

    public async Task<bool> AreaExistsAsync(int areaId)
    {
        return await _context.Areas.AnyAsync(a => a.Id == areaId);
    }

    // The three searches lower-case both sides rather than trusting the database
    // collation, so the same query returns the same order on any server. Ranking
    // the prefix matches first here as well as in the handler matters once a table
    // holds more rows than the limit: it is what decides which rows are dropped.
    public async Task<List<City>> SearchCitiesAsync(string term, int limit)
    {
        return await _context.Cities
            .AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(term))
            .OrderBy(c => c.Name.ToLower().StartsWith(term) ? 0 : 1)
            .ThenBy(c => c.Name)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Area>> SearchAreasAsync(string term, int limit)
    {
        return await _context.Areas
            .AsNoTracking()
            .Include(a => a.City)
            .Where(a => a.Name.ToLower().Contains(term))
            .OrderBy(a => a.Name.ToLower().StartsWith(term) ? 0 : 1)
            .ThenBy(a => a.Name)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Landmark?> GetLandmarkAsync(int landmarkId)
    {
        return await _context.Landmarks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == landmarkId);
    }

    public async Task<List<Landmark>> SearchLandmarksAsync(string term, int limit)
    {
        return await _context.Landmarks
            .AsNoTracking()
            .Include(l => l.City)
            .Where(l => l.Name.ToLower().Contains(term))
            .OrderBy(l => l.Name.ToLower().StartsWith(term) ? 0 : 1)
            .ThenBy(l => l.Name)
            .Take(limit)
            .ToListAsync();
    }
}
