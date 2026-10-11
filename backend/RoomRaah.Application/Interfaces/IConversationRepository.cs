using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IConversationRepository
{
    Task CreateAsync(Conversation conversation);

    /// <summary>
    /// One conversation with its listing and both participants attached. Every caller
    /// needs all three: the participants for rule 102, and the listing title for the
    /// list, the push and the nudge email.
    /// </summary>
    Task<Conversation?> GetByIdAsync(int id);

    /// <summary>
    /// Rule 43's uniqueness, asked before opening one. This is what lets
    /// <c>POST /conversations</c> hand back the thread that already exists instead of
    /// refusing a caller who has simply lost track of an id.
    /// </summary>
    Task<Conversation?> GetForSeekerAndPropertyAsync(int seekerId, int propertyId);

    /// <summary>
    /// Rule 101: the caller's own threads, most recent message first — leaving out any
    /// the caller has deleted and nobody has written in since (rules 129 and 131).
    /// </summary>
    Task<List<Conversation>> GetForUserAsync(int userId, int page, int pageSize);

    Task<int> CountForUserAsync(int userId);
}
