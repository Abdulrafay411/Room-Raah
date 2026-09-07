using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class Landmark
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public LandmarkKind Kind { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public City City { get; set; } = null!;
}
