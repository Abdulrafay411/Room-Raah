using System.Collections.Concurrent;

namespace RoomRaah.API.Realtime;

/// <summary>
/// Who is connected, counted rather than flagged.
///
/// A person with the site open in two tabs holds two connections, and closing one of
/// them does not make them offline — a boolean would say it did, and the next message
/// would go to their inbox instead of their screen. So this counts, and only a count
/// that reaches zero means offline.
///
/// **This is correct for one API instance and is written down rather than assumed.**
/// A second instance would need this in Redis, because neither could see the other's
/// connections. Until there is one, an in-memory count is the honest implementation of
/// a question only the hub can answer, and the cost of ever being wrong is a nudge
/// that does not go out — never one that goes to the wrong person, and never a message
/// that is lost, because the message is in the database either way.
/// </summary>
public class ChatPresence
{
    private readonly ConcurrentDictionary<int, int> _connections = new();

    public void Connected(int userId) =>
        _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);

    public void Disconnected(int userId)
    {
        // AddOrUpdate then remove-at-zero, rather than a lock. The worst a race here
        // can do is leave a zero entry behind, which IsOnline already reads as offline.
        var remaining = _connections.AddOrUpdate(userId, 0, (_, count) => count - 1);

        if (remaining <= 0)
        {
            _connections.TryRemove(userId, out _);
        }
    }

    public bool IsOnline(int userId) =>
        _connections.TryGetValue(userId, out var count) && count > 0;
}
