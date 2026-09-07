using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Messaging;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class MarkConversationReadCommandHandler
    : IRequestHandler<MarkConversationReadCommand, MarkReadResultDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;

    public MarkConversationReadCommandHandler(
        IConversationRepository conversations,
        IMessageRepository messages)
    {
        _conversations = conversations;
        _messages = messages;
    }

    public async Task<MarkReadResultDto> Handle(
        MarkConversationReadCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId)
            ?? throw new NotFoundException(MessagingMessages.ConversationNotFound);

        // Rule 102.
        ConversationAccess.RequireParticipant(conversation, request.ReaderId);

        // Rule 104. A thread with nothing unread marks nothing, and that is a success:
        // a client that opens a conversation twice is not making a mistake the second
        // time, and a refusal there would be a refusal for doing the ordinary thing.
        //
        // There is no SaveChanges here on purpose. MarkReadAsync is a single UPDATE
        // that runs when it is called rather than a change staged on the tracker, so a
        // unit of work after it would commit nothing and would only mislead the next
        // person to read this.
        var marked = await _messages.MarkReadAsync(
            conversation.Id, request.ReaderId, DateTime.UtcNow);

        return new MarkReadResultDto
        {
            ConversationId = conversation.Id,
            MarkedCount = marked
        };
    }
}
