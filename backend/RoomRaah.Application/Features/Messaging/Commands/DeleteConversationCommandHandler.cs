using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Messaging;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class DeleteConversationCommandHandler : IRequestHandler<DeleteConversationCommand>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteConversationCommandHandler> _logger;

    public DeleteConversationCommandHandler(
        IConversationRepository conversations,
        IMessageRepository messages,
        IUnitOfWork unitOfWork,
        ILogger<DeleteConversationCommandHandler> logger)
    {
        _conversations = conversations;
        _messages = messages;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId)
            ?? throw new NotFoundException(MessagingMessages.ConversationNotFound);

        // Rule 129: either participant, nobody else. A stranger deleting a thread from
        // their own inbox would change nothing, but answering 204 would tell them it exists.
        ConversationAccess.RequireParticipant(conversation, request.UserId);

        var now = DateTime.UtcNow;

        // Rules 129 and 130. Only the caller's side moves; deleting again moves the line
        // forward and is not a refusal.
        ConversationAccess.Delete(conversation, request.UserId, now);

        await _unitOfWork.SaveChangesAsync();

        // Rule 132. What was deleted can no longer be read, so it must not stay unread
        // either. MarkReadAsync runs as one UPDATE when it is called, so it needs no
        // SaveChanges of its own.
        await _messages.MarkReadAsync(conversation.Id, request.UserId, now);

        _logger.LogInformation(
            "User {UserId} deleted conversation {ConversationId} from their inbox",
            request.UserId, conversation.Id);
    }
}
