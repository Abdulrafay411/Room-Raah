namespace RoomRaah.Domain.Entities;

public class Area
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public City City { get; set; } = null!;
}
