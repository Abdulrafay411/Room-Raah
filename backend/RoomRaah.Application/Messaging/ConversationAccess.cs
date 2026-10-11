using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Messaging;

/// <summary>
/// Rule 102, in one place.
///
/// Five endpoints ask the same question — may this caller read this thread, list its
/// messages, post into it, mark it read or delete it — and a rule asked in five places
/// is a rule with five chances to be asked slightly differently. It is a static method
/// with one test over both participants and a stranger, rather than an <c>if</c> copied
/// out five times, for the same reason <c>VisitLifecycle</c> is. Rules 129 to 131 live
/// here too, because a delete is always one participant's and choosing which side's
/// fields to touch is the same question asked again.
/// </summary>
public static class ConversationAccess
{
    public static bool IsParticipant(Conversation conversation, int userId) =>
        conversation.SeekerId == userId || conversation.OwnerId == userId;

    public static void RequireParticipant(Conversation conversation, int userId)
    {
        if (!IsParticipant(conversation, userId))
        {
            throw new UnauthorizedAccessException(MessagingMessages.NotAParticipant);
        }
    }

    /// <summary>
    /// The other side of a thread. There are exactly two participants, so "the other
    /// one" is total — and it is the only way the product ever names a participant to
    /// the person they are talking to.
    /// </summary>
    public static int Other(Conversation conversation, int userId) =>
        conversation.SeekerId == userId ? conversation.OwnerId : conversation.SeekerId;

    public static string OtherDisplayName(Conversation conversation, int userId) =>
        conversation.SeekerId == userId
            ? conversation.Owner.FullName
            : conversation.Seeker.FullName;

    /// <summary>
    /// Rule 130: the moment this participant last deleted the thread, or null if they
    /// never have. Nothing sent at or before it is shown to them.
    /// </summary>
    public static DateTime? DeletedAt(Conversation conversation, int userId) =>
        conversation.SeekerId == userId ? conversation.SeekerDeletedAt : conversation.OwnerDeletedAt;

    /// <summary>
    /// Rule 129. Out of this participant's inbox, and everything so far behind the line.
    /// The other participant's two fields are not touched — that copy is the record.
    /// </summary>
    public static void Delete(Conversation conversation, int userId, DateTime at)
    {
        if (conversation.SeekerId == userId)
        {
            conversation.SeekerDeletedAt = at;
            conversation.HiddenFromSeeker = true;
        }
        else
        {
            conversation.OwnerDeletedAt = at;
            conversation.HiddenFromOwner = true;
        }
    }

    /// <summary>
    /// Rule 131. Back in this participant's inbox. The delete moment stays where it is,
    /// so what they deleted stays deleted and only what comes after shows.
    /// </summary>
    public static void Reveal(Conversation conversation, int userId)
    {
        if (conversation.SeekerId == userId)
        {
            conversation.HiddenFromSeeker = false;
        }
        else
        {
            conversation.HiddenFromOwner = false;
        }
    }
}
