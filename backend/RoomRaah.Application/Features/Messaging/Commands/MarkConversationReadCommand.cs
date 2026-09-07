using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class MarkConversationReadCommand : IRequest<MarkReadResultDto>
{
    /// <summary>Set from the route by the controller.</summary>
    public int ConversationId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int ReaderId { get; set; }
}
