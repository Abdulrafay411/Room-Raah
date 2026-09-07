using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly RoomRaahDbContext _context;

    public ReviewRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Review review)
    {
        _context.Reviews.Add(review);
        return Task.CompletedTask;
    }

    /// <summary>Rule 39: whatever state the earlier review is in, it counts.</summary>
    public Task<bool> ExistsForAsync(int authorId, int propertyId)
    {
        return _context.Reviews.AnyAsync(r => r.AuthorId == authorId && r.PropertyId == propertyId);
    }

    public async Task<Review?> GetByIdAsync(int id)
    {
        return await _context.Reviews
            .Include(r => r.Author)
            .Include(r => r.Property)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public Task<List<Review>> GetApprovedAsync(int propertyId, int page, int pageSize)
    {
        return Approved(propertyId)
            .Include(r => r.Author)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountApprovedAsync(int propertyId)
    {
        return Approved(propertyId).CountAsync();
    }

    /// <summary>
    /// Cast to a nullable before averaging so that a listing with no approved reviews
    /// comes back as null rather than throwing. Null is the honest answer: a listing
    /// nobody has rated does not have a rating of zero.
    /// </summary>
    public Task<double?> AverageApprovedAsync(int propertyId)
    {
        return Approved(propertyId).AverageAsync(r => (double?)r.Rating);
    }

    public Task<List<Review>> GetQueueAsync(ModerationState? state, int page, int pageSize)
    {
        return Queue(state)
            .Include(r => r.Author)
            .Include(r => r.Property)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountQueueAsync(ModerationState? state)
    {
        return Queue(state).CountAsync();
    }

    /// <summary>
    /// Rule 41 in one expression. Every public read goes through it, so a new one
    /// cannot forget that a review is invisible until an admin has read it.
    /// </summary>
    private IQueryable<Review> Approved(int propertyId)
    {
        return _context.Reviews.Where(r =>
            r.PropertyId == propertyId && r.ModerationState == ModerationState.Approved);
    }

    private IQueryable<Review> Queue(ModerationState? state)
    {
        var query = _context.Reviews.AsQueryable();

        if (state is { } wanted)
        {
            query = query.Where(r => r.ModerationState == wanted);
        }

        return query;
    }
}
