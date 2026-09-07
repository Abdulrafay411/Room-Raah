namespace RoomRaah.Domain.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser AppUser { get; set; } = null!;
}
