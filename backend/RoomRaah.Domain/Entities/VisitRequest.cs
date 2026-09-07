using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

/// <summary>
/// A seeker asking to see a room. This is the only way a visit is arranged in the
/// product — there is no phone number to ring — so the row carries the whole
/// conversation: what kind of visit, when, what the owner said and when they said it.
/// </summary>
public class VisitRequest
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int SeekerId { get; set; }
    public VisitType VisitType { get; set; }
    public DateTime PreferredAt { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Requested;

    /// <summary>Rule 89: required when the owner declines, optional when they accept.</summary>
    public string? OwnerResponseNote { get; set; }

    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// When the request was made, which is not <see cref="PreferredAt"/> and is what
    /// both lists are ordered by. A queue ordered by the visit date would put a
    /// request made this morning for next month behind one made last week for tomorrow.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser Seeker { get; set; } = null!;
}
