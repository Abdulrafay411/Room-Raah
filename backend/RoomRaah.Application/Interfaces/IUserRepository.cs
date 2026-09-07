using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IUserRepository
{
    Task CreateAsync(AppUser user);
    Task<AppUser?> GetByEmailAsync(string email);
    Task<AppUser?> GetByIdAsync(int id);
    Task<bool> EmailExistsAsync(string email);
    Task UpdateAsync(AppUser user);

    /// <summary>
    /// Rule 117. Every account including the deactivated ones, which is the whole
    /// point of an admin user list — filtered by role and by active state, searchable
    /// by name or email, newest first.
    /// </summary>
    Task<List<AppUser>> GetPageAsync(
        UserRole? role, bool? isActive, string? search, int page, int pageSize);

    Task<int> CountPageAsync(UserRole? role, bool? isActive, string? search);
}
