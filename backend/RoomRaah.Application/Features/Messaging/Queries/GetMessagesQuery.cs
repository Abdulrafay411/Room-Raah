using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Messaging.Queries;

public class GetMessagesQuery : IRequest<PagedResult<MessageDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public int ConversationId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int UserId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}
