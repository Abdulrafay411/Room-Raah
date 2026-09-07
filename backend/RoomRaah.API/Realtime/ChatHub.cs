using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RoomRaah.API.Realtime;

/// <summary>
/// The live half of messaging. It lives in the API project because a hub is an
/// endpoint — the same reason a controller does.
///
/// **It has no methods a client can call.** Sending a message is
/// <c>POST /conversations/{id}/messages</c>, which is where the validation, rule 102
/// and the persistence already are; adding a second way in would be a second place for
/// all three to be got wrong. This hub does one thing: it holds a connection open so
/// the server can push to it, and it tells <see cref="ChatPresence"/> when that
/// connection opens and closes.
///
/// Rule 106 is enforced by never having a group to leak through. A connection is only
/// ever addressed as its own user, so there is nothing to subscribe to and nothing to
/// guess the name of.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ChatPresence _presence;

    public ChatHub(ChatPresence presence)
    {
        _presence = presence;
    }

    public override Task OnConnectedAsync()
    {
        _presence.Connected(UserId());

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presence.Disconnected(UserId());

        return base.OnDisconnectedAsync(exception);
    }

    private int UserId() =>
        int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
