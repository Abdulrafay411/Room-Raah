using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
    Task<bool> IsRefreshTokenRevoked(string token);
    Task RevokeRefreshTokenAsync(string token, TimeSpan remainingLifetime);
}
