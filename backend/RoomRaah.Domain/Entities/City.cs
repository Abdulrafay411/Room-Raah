namespace RoomRaah.Domain.Entities;

public class City
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Area> Areas { get; set; } = new List<Area>();
    public ICollection<Landmark> Landmarks { get; set; } = new List<Landmark>();
}
