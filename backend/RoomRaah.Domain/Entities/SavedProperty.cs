namespace RoomRaah.Domain.Entities;

/// <summary>
/// One listing on one person's shortlist. Rule 34 makes the pair unique, and the
/// row outlives the listing's publication: a listing can be unpublished and come
/// back, and a shortlist that quietly forgot it would be lying to the person who
/// built it.
/// </summary>
public class SavedProperty
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PropertyId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    public AppUser User { get; set; } = null!;
    public Property Property { get; set; } = null!;
}
