using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Messaging;

/// <summary>
/// Rule 102, in one place.
///
/// Four endpoints ask the same question — may this caller read this thread, list its
/// messages, post into it or mark it read — and a rule asked in four places is a rule
/// with four chances to be asked slightly differently. It is a static method with one
/// test over both participants and a stranger, rather than an <c>if</c> copied out
/// four times, for the same reason <c>VisitLifecycle</c> is.
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
}
