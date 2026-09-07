using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class OtpRepository : IOtpRepository
{
    private readonly RoomRaahDbContext _context;

    public OtpRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(OtpCode code)
    {
        _context.OtpCodes.Add(code);
        return Task.CompletedTask;
    }

    public async Task<OtpCode?> GetLatestActiveAsync(int appUserId, OtpPurpose purpose)
    {
        return await _context.OtpCodes
            .IgnoreQueryFilters()
            .Where(c => c.AppUserId == appUserId && c.Purpose == purpose && c.ConsumedAt == null)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task InvalidateActiveAsync(int appUserId, OtpPurpose purpose)
    {
        var active = await _context.OtpCodes
            .IgnoreQueryFilters()
            .Where(c => c.AppUserId == appUserId && c.Purpose == purpose && c.ConsumedAt == null)
            .ToListAsync();

        foreach (var code in active)
        {
            code.ConsumedAt = DateTime.UtcNow;
        }
    }

    public Task UpdateAsync(OtpCode code)
    {
        _context.OtpCodes.Update(code);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rule 126. The cutoff applies to both halves: a code consumed a minute ago is
    /// spent, but it is also the only record that a verification happened, and the
    /// support question about it arrives days later rather than seconds later.
    ///
    /// ExecuteDelete rather than loading the rows to remove them — the answer is a
    /// count, so there is nothing to bring back, and a cleanup that has to materialise
    /// its own backlog is the shape that fails on the day the backlog is real. It runs
    /// when it is called, so there is no SaveChanges after it.
    /// </summary>
    public Task<int> DeleteExpiredAsync(DateTime cutoff)
    {
        return _context.OtpCodes
            .IgnoreQueryFilters()
            .Where(c => c.ExpiresAt < cutoff || (c.ConsumedAt != null && c.ConsumedAt < cutoff))
            .ExecuteDeleteAsync();
    }
}
