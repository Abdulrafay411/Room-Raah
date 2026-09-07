using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

public class VerificationCheck
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public VerificationCheckType CheckType { get; set; }
    public VerificationStatus Status { get; set; }
    public DateTime? EvidenceDate { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? Note { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
}
