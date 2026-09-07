using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

/// <summary>
/// A visitor telling the admin team that something on a listing is wrong. Rule 98:
/// upholding one records a verdict and does not itself take the listing down —
/// a listing's status changes because an admin decided to change it.
/// </summary>
public class Report
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int ReportedByUserId { get; set; }
    public ReportReason Reason { get; set; }
    public string Details { get; set; } = string.Empty;
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public int? ResolvedByAdminId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser ReportedByUser { get; set; } = null!;
}
