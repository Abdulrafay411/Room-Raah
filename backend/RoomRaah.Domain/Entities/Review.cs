using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

/// <summary>
/// What someone who actually saw the room thought of it. Rule 38 is what makes it
/// worth reading — a review can only be written after a visit request on the
/// listing reached <c>Completed</c> — and rule 41 is what keeps it safe: every
/// review starts <c>Pending</c> and is invisible until an admin approves it.
/// </summary>
public class Review
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int AuthorId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public StayStatus StayStatus { get; set; }
    public ModerationState ModerationState { get; set; } = ModerationState.Pending;

    /// <summary>Which admin decided, and when. The same stamp every other decision in the product carries.</summary>
    public int? ModeratedByAdminId { get; set; }

    public DateTime? ModeratedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser Author { get; set; } = null!;
}
