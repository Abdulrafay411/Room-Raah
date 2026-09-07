using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly RoomRaahDbContext _context;

    public ReportRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Report report)
    {
        _context.Reports.Add(report);
        return Task.CompletedTask;
    }

    /// <summary>Rule 42 as amended: Open and UnderReview both count as unresolved.</summary>
    public Task<bool> HasUnresolvedAsync(int userId, int propertyId)
    {
        return _context.Reports.AnyAsync(r =>
            r.ReportedByUserId == userId &&
            r.PropertyId == propertyId &&
            (r.Status == ReportStatus.Open || r.Status == ReportStatus.UnderReview));
    }

    public async Task<Report?> GetByIdAsync(int id)
    {
        return await _context.Reports
            .Include(r => r.Property)
            .Include(r => r.ReportedByUser)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public Task<List<Report>> GetQueueAsync(ReportStatus? status, int page, int pageSize)
    {
        return Queue(status)
            .Include(r => r.Property)
            .Include(r => r.ReportedByUser)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountQueueAsync(ReportStatus? status)
    {
        return Queue(status).CountAsync();
    }

    private IQueryable<Report> Queue(ReportStatus? status)
    {
        var query = _context.Reports.AsQueryable();

        if (status is { } wanted)
        {
            query = query.Where(r => r.Status == wanted);
        }

        return query;
    }
}
