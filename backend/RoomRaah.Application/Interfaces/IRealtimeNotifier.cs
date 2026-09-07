using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// The push side of messaging, declared here and implemented in the API project over
/// <c>IHubContext&lt;ChatHub&gt;</c>. It lives on this side of the wall so that a
/// handler can push without the Application layer knowing what SignalR is, and so
/// that Infrastructure never has to reference ASP.NET Core to get at it.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Delivers a message to one user's own connections and to nobody else's. Rule 106
    /// is enforced by addressing the recipient directly: there is no group in this
    /// product that a connection can join and receive somebody else's thread from.
    /// </summary>
    Task MessageReceivedAsync(int recipientUserId, MessageDto message);

    /// <summary>
    /// Whether the user holds a live hub connection. The hub is the only thing in the
    /// product that knows — it is told when a connection opens and closes, where
    /// everything else would be guessing from a last-seen stamp.
    ///
    /// This is correct for one API instance. A second would need presence in Redis,
    /// because neither instance can see the other's connections; the cost of being
    /// wrong is a nudge that does not go out, never one that goes to the wrong person.
    /// </summary>
    Task<bool> IsOnlineAsync(int userId);
}
