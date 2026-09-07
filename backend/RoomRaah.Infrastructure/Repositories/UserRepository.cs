using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RoomRaahDbContext _context;

    public UserRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(AppUser user)
    {
        _context.AppUsers.Add(user);
        return Task.CompletedTask;
    }

    public async Task<AppUser?> GetByEmailAsync(string email)
    {
        return await WithProfiles()
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<AppUser?> GetByIdAsync(int id)
    {
        return await WithProfiles()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.AppUsers
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email);
    }

    public Task UpdateAsync(AppUser user)
    {
        _context.AppUsers.Update(user);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rule 117. IgnoreQueryFilters on purpose: the soft-delete filter would hide
    /// exactly the accounts this list exists to show.
    /// </summary>
    public Task<List<AppUser>> GetPageAsync(
        UserRole? role, bool? isActive, string? search, int page, int pageSize)
    {
        return Matching(role, isActive, search)
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountPageAsync(UserRole? role, bool? isActive, string? search)
    {
        return Matching(role, isActive, search).CountAsync();
    }

    /// <summary>
    /// The filters, in one place so the page and the count can never answer different
    /// questions — the same reason the shortlist and the conversation list hold theirs
    /// this way.
    /// </summary>
    private IQueryable<AppUser> Matching(UserRole? role, bool? isActive, string? search)
    {
        var query = _context.AppUsers.IgnoreQueryFilters();

        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }

        if (isActive is not null)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                EF.Functions.Like(u.FullName, $"%{search}%") ||
                EF.Functions.Like(u.Email, $"%{search}%"));
        }

        return query;
    }

    private IQueryable<AppUser> WithProfiles()
    {
        return _context.AppUsers
            .IgnoreQueryFilters()
            .Include(u => u.SeekerProfile)
            .Include(u => u.OwnerProfile)
            .Include(u => u.AdminProfile);
    }
}
