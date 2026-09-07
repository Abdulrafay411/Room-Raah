using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class Property
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int AreaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public RoomType RoomType { get; set; }
    public GenderPolicy GenderPolicy { get; set; }
    public int TotalBeds { get; set; }
    public int AvailableBeds { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal UtilitiesCharge { get; set; }
    public decimal MessCharge { get; set; }
    public string? HouseRules { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Draft;
    public bool HasInspectionBadge { get; set; }
    public DateTime? LastInspectionAt { get; set; }

    /// <summary>
    /// Rule 108: the owner asks once, and only once. Stamped here rather than held as
    /// a queue entry, because after the first visit the team goes on its own schedule
    /// and no request is involved. Rule 115 is why there is no companion field saying
    /// when the next visit is: no such date exists anywhere in the product.
    /// </summary>
    public DateTime? InspectionRequestedAt { get; set; }
    public DateTime AvailabilityConfirmedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppUser Owner { get; set; } = null!;
    public Area Area { get; set; } = null!;
    public ICollection<PropertyPhoto> Photos { get; set; } = new List<PropertyPhoto>();
    public ICollection<PropertyFacility> Facilities { get; set; } = new List<PropertyFacility>();
    public ICollection<PropertyRevision> Revisions { get; set; } = new List<PropertyRevision>();
    public ICollection<VerificationCheck> VerificationChecks { get; set; } = new List<VerificationCheck>();
    public ICollection<PropertyInspection> Inspections { get; set; } = new List<PropertyInspection>();
    public ICollection<BadgeEvent> BadgeEvents { get; set; } = new List<BadgeEvent>();
}
