using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class PropertyRevision
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public RevisionKind Kind { get; set; }
    public RevisionStatus ReviewStatus { get; set; } = RevisionStatus.Pending;

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

    public string FacilityIds { get; set; } = string.Empty;
    public string? PhotoOrder { get; set; }
    public int? PrimaryPhotoId { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? AdminNote { get; set; }

    public Property Property { get; set; } = null!;
}
