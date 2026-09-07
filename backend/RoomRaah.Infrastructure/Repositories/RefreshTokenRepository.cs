using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly RoomRaahDbContext _context;

    public RefreshTokenRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task RevokeAllForUserAsync(int appUserId)
    {
        var active = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.AppUserId == appUserId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in active)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
    }
}
