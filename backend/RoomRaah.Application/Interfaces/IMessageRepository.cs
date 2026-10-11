using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IMessageRepository
{
    Task CreateAsync(Message message);

    /// <summary>
    /// A page of one thread, oldest first — the order a conversation is read in. With
    /// <paramref name="sentAfter"/>, only what was sent after it: rule 130's line for a
    /// participant who deleted the thread.
    /// </summary>
    Task<List<Message>> GetForConversationAsync(
        int conversationId, int page, int pageSize, DateTime? sentAfter = null);

    Task<int> CountForConversationAsync(int conversationId, DateTime? sentAfter = null);

    /// <summary>
    /// Rule 104. Marks every message in the thread the caller did not send and has not
    /// already read, and answers with how many it marked. Written as one update rather
    /// than a read-then-write loop: the count is the answer, so there is nothing to
    /// load, and a thread with two hundred messages is still one statement.
    /// </summary>
    Task<int> MarkReadAsync(int conversationId, int readerId, DateTime readAt);

    /// <summary>
    /// The unread count for each of the given threads, from the caller's side. One
    /// query for a whole page of conversations, because the alternative is a count per
    /// row and a list of twenty threads then costs twenty round trips.
    /// </summary>
    Task<Dictionary<int, int>> GetUnreadCountsAsync(int readerId, IReadOnlyCollection<int> conversationIds);
}
