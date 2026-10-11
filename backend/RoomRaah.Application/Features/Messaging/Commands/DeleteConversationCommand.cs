using MediatR;

namespace RoomRaah.Application.Features.Messaging.Commands;

/// <summary>
/// Rule 129. Removes a thread from the caller's inbox only — the other participant keeps
/// their copy, and no row is deleted.
/// </summary>
public class DeleteConversationCommand : IRequest
{
    /// <summary>Set from the route by the controller.</summary>
    public int ConversationId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int UserId { get; set; }
}
