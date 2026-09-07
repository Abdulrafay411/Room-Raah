using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class PropertyRevisionRepository : IPropertyRevisionRepository
{
    private readonly RoomRaahDbContext _context;

    public PropertyRevisionRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(PropertyRevision revision)
    {
        _context.PropertyRevisions.Add(revision);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The whole graph an approval touches: the listing, its photos, its facilities
    /// and the owner the decision is emailed to.
    /// </summary>
    public async Task<PropertyRevision?> GetByIdAsync(int id)
    {
        return await _context.PropertyRevisions
            .Include(r => r.Property).ThenInclude(p => p.Area).ThenInclude(a => a.City)
            .Include(r => r.Property).ThenInclude(p => p.Photos)
            .Include(r => r.Property).ThenInclude(p => p.Facilities).ThenInclude(f => f.Facility)
            .Include(r => r.Property).ThenInclude(p => p.Owner)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<PropertyRevision?> GetPendingAsync(int propertyId, RevisionKind kind)
    {
        return await _context.PropertyRevisions
            .FirstOrDefaultAsync(r =>
                r.PropertyId == propertyId &&
                r.Kind == kind &&
                r.ReviewStatus == RevisionStatus.Pending);
    }

    /// <summary>
    /// Oldest first: an admin opens a queue to clear what has been waiting longest,
    /// which is the opposite of what an owner wants from their own dashboard.
    /// </summary>
    public async Task<List<PropertyRevision>> GetQueueAsync(RevisionKind? kind, int page, int pageSize)
    {
        return await Queue(kind)
            .AsNoTracking()
            .Include(r => r.Property).ThenInclude(p => p.Area).ThenInclude(a => a.City)
            .Include(r => r.Property).ThenInclude(p => p.Owner)
            .OrderBy(r => r.SubmittedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountQueueAsync(RevisionKind? kind)
    {
        return await Queue(kind).CountAsync();
    }

    private IQueryable<PropertyRevision> Queue(RevisionKind? kind)
    {
        var query = _context.PropertyRevisions
            .Where(r => r.ReviewStatus == RevisionStatus.Pending);

        return kind is null ? query : query.Where(r => r.Kind == kind);
    }
}
