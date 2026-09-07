namespace RoomRaah.Application.DTOs;

/// <summary>
/// One thread as one of its two participants sees it. Rule 33 and rule 93 again, in
/// the one place in the product where two people talk directly: the other participant
/// is a display name and nothing else, whichever side is reading.
/// </summary>
public class ConversationDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing the conversation is about.</summary>
    public int PropertyId { get; set; }

    /// <summary>Its title, so an inbox is readable without a second request per row.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>
    /// The name the other person signed up with. Which person that is depends on who
    /// is reading, so the handler fills this — there is no owner-shaped field and no
    /// seeker-shaped field, because a shape that names both would leak one of them to
    /// the other on the day somebody forgot to filter it.
    /// </summary>
    public string OtherParticipantDisplayName { get; set; } = string.Empty;

    /// <summary>When the conversation was opened.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Rule 103: the most recent message, and the order of the list.</summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>How many messages the caller has not read yet.</summary>
    public int UnreadCount { get; set; }
}

/// <summary>
/// One message. The sender is a display name and a side, never an id — a client
/// renders a thread by knowing which messages are its own, which is what
/// <see cref="IsMine"/> answers without handing over anything to identify anyone with.
/// </summary>
public class MessageDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The thread it belongs to.</summary>
    public int ConversationId { get; set; }

    /// <summary>The name the sender signed up with.</summary>
    public string SenderDisplayName { get; set; } = string.Empty;

    /// <summary>Whether the caller wrote it.</summary>
    public bool IsMine { get; set; }

    /// <summary>What was written.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>When it was sent. A thread is ordered by this, oldest first.</summary>
    public DateTime SentAt { get; set; }

    /// <summary>When the other participant read it, if they have.</summary>
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// What <c>PATCH /conversations/{id}/read</c> answers with. Rule 104: marking a thread
/// that holds nothing unread marks nothing and is not a refusal, so the count is the
/// whole reply — a client showing an unread badge needs to know it went to zero.
/// </summary>
public class MarkReadResultDto
{
    /// <summary>The thread that was marked.</summary>
    public int ConversationId { get; set; }

    /// <summary>How many messages this call marked as read. Zero is a success.</summary>
    public int MarkedCount { get; set; }
}
