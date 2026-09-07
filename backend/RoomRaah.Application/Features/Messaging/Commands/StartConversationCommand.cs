using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class StartConversationCommand : IRequest<StartConversationResult>
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int SeekerId { get; set; }

    public int PropertyId { get; set; }
}

/// <summary>
/// Rule 100. Opening a conversation that already exists is not a mistake and is not
/// refused — the caller is handed the thread that is already there, and told which of
/// the two happened so the controller can answer 201 or 200.
///
/// The alternative was to make the client ask whether a conversation existed before it
/// could open one, which is a round trip to avoid a case that is not an error.
/// </summary>
public record StartConversationResult(ConversationDto Conversation, bool WasCreated);
