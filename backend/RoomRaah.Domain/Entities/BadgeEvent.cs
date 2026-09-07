using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

/// <summary>
/// The badge's audit trail. Rule 112: every grant and every removal records who, when
/// and why.
///
/// The badge has no expiry and comes off only when an admin decides, which is exactly
/// what makes this necessary — a claim RoomRaah makes about a property, kept alive by
/// a person, needs to say who that person was. §Business Model puts it plainly: a badge
/// that quietly disappears is worse than no badge.
/// </summary>
public class BadgeEvent
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public BadgeAction Action { get; set; }

    /// <summary>Required on both actions. Rule 112.</summary>
    public string Reason { get; set; } = string.Empty;

    public int ActedByAdminId { get; set; }
    public DateTime ActedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser ActedByAdmin { get; set; } = null!;
}
