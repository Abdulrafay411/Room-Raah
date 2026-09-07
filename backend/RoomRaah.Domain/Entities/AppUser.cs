using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsEmailVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SeekerProfile? SeekerProfile { get; set; }
    public OwnerProfile? OwnerProfile { get; set; }
    public AdminProfile? AdminProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<OtpCode> OtpCodes { get; set; } = new List<OtpCode>();
}
