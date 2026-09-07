using Microsoft.AspNetCore.Identity;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string Hash(string password)
    {
        return _hasher.HashPassword(new AppUser(), password);
    }

    public bool Verify(string hashedPassword, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(new AppUser(), hashedPassword, providedPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
