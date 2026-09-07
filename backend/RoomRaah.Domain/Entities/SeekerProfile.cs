using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class SeekerProfile
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public int? PreferredCityId { get; set; }
    public int? PreferredLandmarkId { get; set; }
    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }
    public GenderPolicy? GenderPreference { get; set; }

    public AppUser AppUser { get; set; } = null!;
}
