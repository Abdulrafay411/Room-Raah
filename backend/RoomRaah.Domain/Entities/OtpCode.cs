using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class OtpCode
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public OtpPurpose Purpose { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser AppUser { get; set; } = null!;
}
