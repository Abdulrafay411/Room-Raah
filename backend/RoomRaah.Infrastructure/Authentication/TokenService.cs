using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    public const string EmailVerifiedClaim = "email_verified";

    private readonly JwtSettings _jwtSettings;
    private readonly ICacheService _cacheService;

    public TokenService(IOptions<JwtSettings> jwtSettings, ICacheService cacheService)
    {
        _jwtSettings = jwtSettings.Value;
        _cacheService = cacheService;
    }

    public string GenerateAccessToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(EmailVerifiedClaim, user.IsEmailVerified ? "true" : "false")
        };

        foreach (var role in RolesFor(user))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public async Task<bool> IsRefreshTokenRevoked(string token)
    {
        var value = await _cacheService.GetAsync<string>(RevocationKey(token));
        return value is not null;
    }

    public async Task RevokeRefreshTokenAsync(string token, TimeSpan remainingLifetime)
    {
        await _cacheService.SetAsync(RevocationKey(token), "1", remainingLifetime);
    }

    /// <summary>
    /// The key is a digest of the token, never the token itself. Cache keys are written
    /// to the log by the cache service, and a refresh token in a log file is a refresh
    /// token an attacker can use. Hashing also keeps live tokens out of a Redis dump.
    /// </summary>
    private static string RevocationKey(string token)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return "revoked:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>
    /// The account's own role, plus a role for every profile it holds. One person can
    /// hold both an owner and a seeker profile, and the token has to say so.
    /// </summary>
    private static IEnumerable<string> RolesFor(AppUser user)
    {
        var roles = new HashSet<string> { user.Role.ToString() };

        if (user.SeekerProfile is not null) roles.Add(UserRole.Seeker.ToString());
        if (user.OwnerProfile is not null) roles.Add(UserRole.Owner.ToString());
        if (user.AdminProfile is not null) roles.Add(UserRole.Admin.ToString());

        return roles;
    }
}
