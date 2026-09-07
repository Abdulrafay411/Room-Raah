namespace RoomRaah.Domain.Entities;

/// <summary>
/// One message in a conversation. Stored in SQL Server like everything else — SignalR
/// only pushes a copy to whoever is connected, it never holds the history.
/// </summary>
public class Message
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }

    /// <summary>
    /// What was written. §4.2's never-log list covers this: the product records that a
    /// message was sent and never a character of what it said.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null until the other participant marks the conversation read.</summary>
    public DateTime? ReadAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
    public AppUser Sender { get; set; } = null!;
}
