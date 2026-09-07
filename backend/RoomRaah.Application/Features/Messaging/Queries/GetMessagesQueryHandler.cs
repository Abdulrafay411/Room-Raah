using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Messaging;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Messaging.Queries;

public class GetMessagesQueryHandler : IRequestHandler<GetMessagesQuery, PagedResult<MessageDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IMapper _mapper;

    public GetMessagesQueryHandler(
        IConversationRepository conversations,
        IMessageRepository messages,
        IMapper mapper)
    {
        _conversations = conversations;
        _messages = messages;
        _mapper = mapper;
    }

    public async Task<PagedResult<MessageDto>> Handle(
        GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId)
            ?? throw new NotFoundException(MessagingMessages.ConversationNotFound);

        // Rule 102. Asked before a single message is read, not after.
        ConversationAccess.RequireParticipant(conversation, request.UserId);

        var messages = await _messages.GetForConversationAsync(
            conversation.Id, request.Page, request.PageSize);

        var items = messages.Select(message =>
        {
            var dto = _mapper.Map<MessageDto>(message);

            dto.IsMine = message.SenderId == request.UserId;

            return dto;
        }).ToList();

        return new PagedResult<MessageDto>
        {
            Items = items,
            TotalCount = await _messages.CountForConversationAsync(conversation.Id),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
