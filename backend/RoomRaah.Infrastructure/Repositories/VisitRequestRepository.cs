using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class VisitRequestRepository : IVisitRequestRepository
{
    private readonly RoomRaahDbContext _context;

    public VisitRequestRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(VisitRequest request)
    {
        _context.VisitRequests.Add(request);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tracked, because every caller writes to it. The owner comes with it so rule 37
    /// can be asked without a second read, and the seeker so rule 92's email has
    /// somewhere to go.
    /// </summary>
    public async Task<VisitRequest?> GetByIdAsync(int id)
    {
        return await WithContext()
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    /// <summary>Rule 36.</summary>
    public Task<bool> HasOpenAsync(int seekerId, int propertyId)
    {
        return _context.VisitRequests.AnyAsync(v =>
            v.SeekerId == seekerId &&
            v.PropertyId == propertyId &&
            (v.Status == VisitStatus.Requested || v.Status == VisitStatus.Accepted));
    }

    /// <summary>Rule 38.</summary>
    public Task<bool> HasCompletedAsync(int seekerId, int propertyId)
    {
        return _context.VisitRequests.AnyAsync(v =>
            v.SeekerId == seekerId &&
            v.PropertyId == propertyId &&
            v.Status == VisitStatus.Completed);
    }

    public Task<List<VisitRequest>> GetForSeekerAsync(int seekerId, int page, int pageSize)
    {
        return Page(WithContext().Where(v => v.SeekerId == seekerId), page, pageSize);
    }

    public Task<int> CountForSeekerAsync(int seekerId)
    {
        return _context.VisitRequests.CountAsync(v => v.SeekerId == seekerId);
    }

    public Task<List<VisitRequest>> GetForOwnerAsync(int ownerId, int page, int pageSize)
    {
        return Page(WithContext().Where(v => v.Property.OwnerId == ownerId), page, pageSize);
    }

    public Task<int> CountForOwnerAsync(int ownerId)
    {
        return _context.VisitRequests.CountAsync(v => v.Property.OwnerId == ownerId);
    }

    private IQueryable<VisitRequest> WithContext()
    {
        return _context.VisitRequests
            .Include(v => v.Property).ThenInclude(p => p.Area).ThenInclude(a => a.City)
            .Include(v => v.Property).ThenInclude(p => p.Owner)
            .Include(v => v.Seeker);
    }

    /// <summary>
    /// Ordered by when the request was made and broken by the id, for the reason rule
    /// 77 gives about search: two requests made in the same second are otherwise free
    /// to swap places between one page and the next.
    /// </summary>
    private static Task<List<VisitRequest>> Page(IQueryable<VisitRequest> query, int page, int pageSize)
    {
        return query
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
