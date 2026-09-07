namespace RoomRaah.Domain.Entities;

/// <summary>
/// One thread between a seeker and an owner about one listing. This is what
/// §Contact Stays On the Site promises instead of a phone number, so it is the only
/// channel the two of them have.
///
/// Rule 43 makes it unique per property and seeker, which means the same two people
/// talking about two rooms get two threads — and it is also what lets
/// <c>POST /conversations</c> be safe to call twice.
/// </summary>
public class Conversation
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public int SeekerId { get; set; }

    /// <summary>
    /// Copied from the listing when the conversation opens rather than reached
    /// through it. A listing's owner never changes, and holding the id here means the
    /// participant check — rule 102, asked on every single message — is one column
    /// comparison instead of a join to the property.
    /// </summary>
    public int OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Rule 103. Stamped by every message and used to order both inboxes, so the list
    /// does not have to aggregate over the messages to know what is recent.
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser Seeker { get; set; } = null!;
    public AppUser Owner { get; set; } = null!;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
