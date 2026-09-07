using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Messaging;

namespace RoomRaah.Application.Features.Messaging.Queries;

public class GetConversationsQueryHandler
    : IRequestHandler<GetConversationsQuery, PagedResult<ConversationDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IMapper _mapper;

    public GetConversationsQueryHandler(
        IConversationRepository conversations,
        IMessageRepository messages,
        IMapper mapper)
    {
        _conversations = conversations;
        _messages = messages;
        _mapper = mapper;
    }

    public async Task<PagedResult<ConversationDto>> Handle(
        GetConversationsQuery request, CancellationToken cancellationToken)
    {
        // Rule 101. The repository returns only the caller's own threads, so there is
        // no filtering left to forget here.
        var conversations = await _conversations.GetForUserAsync(
            request.UserId, request.Page, request.PageSize);

        var ids = conversations.Select(c => c.Id).ToList();

        // One query for the whole page. A count per row would make an inbox of twenty
        // threads twenty round trips, which is the shape that only hurts in production.
        var unread = await _messages.GetUnreadCountsAsync(request.UserId, ids);

        var items = conversations.Select(conversation =>
        {
            var dto = _mapper.Map<ConversationDto>(conversation);

            // Whichever of the two the caller is not. This is the only place the
            // product names one participant to the other, and it is a display name.
            dto.OtherParticipantDisplayName =
                ConversationAccess.OtherDisplayName(conversation, request.UserId);

            dto.UnreadCount = unread.GetValueOrDefault(conversation.Id);

            return dto;
        }).ToList();

        return new PagedResult<ConversationDto>
        {
            Items = items,
            TotalCount = await _conversations.CountForUserAsync(request.UserId),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
