using Microsoft.AspNetCore.SignalR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.API.Realtime;

/// <summary>
/// The API-side implementation of <see cref="IRealtimeNotifier"/>. It is a wrapper and
/// nothing more: the decision about whether to push or to email is a business rule and
/// lives in the handler, which is why this class holds none of it.
///
/// Implementing an Application interface from the API project breaks no dependency
/// rule — API already references Application — and it is what keeps ASP.NET Core out of
/// Infrastructure entirely.
/// </summary>
public class RealtimeNotifier : IRealtimeNotifier
{
    /// <summary>The method name the Angular client subscribes to.</summary>
    public const string MessageReceived = "MessageReceived";

    private readonly IHubContext<ChatHub> _hub;
    private readonly ChatPresence _presence;

    public RealtimeNotifier(IHubContext<ChatHub> hub, ChatPresence presence)
    {
        _hub = hub;
        _presence = presence;
    }

    /// <summary>
    /// Addressed to one user, never to a group. SignalR resolves a user id to that
    /// user's own connections, so rule 106 holds without a subscription list to get
    /// wrong: there is no name a connection could join to receive somebody else's
    /// conversation.
    /// </summary>
    public Task MessageReceivedAsync(int recipientUserId, MessageDto message) =>
        _hub.Clients.User(recipientUserId.ToString()).SendAsync(MessageReceived, message);

    public Task<bool> IsOnlineAsync(int userId) =>
        Task.FromResult(_presence.IsOnline(userId));
}
