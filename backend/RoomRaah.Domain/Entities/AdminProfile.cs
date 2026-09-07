namespace RoomRaah.Domain.Entities;

public class AdminProfile
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public bool CanInspect { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser AppUser { get; set; } = null!;
}
