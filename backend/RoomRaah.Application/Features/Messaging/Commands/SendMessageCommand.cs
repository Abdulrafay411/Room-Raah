using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class SendMessageCommand : IRequest<MessageDto>
{
    /// <summary>Set from the route by the controller.</summary>
    public int ConversationId { get; set; }

    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int SenderId { get; set; }

    public string Body { get; set; } = string.Empty;
}
