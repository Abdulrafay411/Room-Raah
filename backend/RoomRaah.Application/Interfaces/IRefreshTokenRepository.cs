using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task RevokeAllForUserAsync(int appUserId);
}
