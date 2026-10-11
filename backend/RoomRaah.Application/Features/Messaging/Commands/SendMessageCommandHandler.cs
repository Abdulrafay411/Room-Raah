using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Messaging;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, MessageDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IRealtimeNotifier _realtime;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<SendMessageCommandHandler> _logger;

    public SendMessageCommandHandler(
        IConversationRepository conversations,
        IMessageRepository messages,
        IRealtimeNotifier realtime,
        ICacheService cache,
        IEmailSender email,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<SendMessageCommandHandler> logger)
    {
        _conversations = conversations;
        _messages = messages;
        _realtime = realtime;
        _cache = cache;
        _email = email;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<MessageDto> Handle(
        SendMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId)
            ?? throw new NotFoundException(MessagingMessages.ConversationNotFound);

        // Rule 102.
        ConversationAccess.RequireParticipant(conversation, request.SenderId);

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = request.SenderId,
            Body = request.Body,
            SentAt = DateTime.UtcNow
        };

        await _messages.CreateAsync(message);

        // Rule 103. Stamped here rather than aggregated at read time, so ordering an
        // inbox does not mean a subquery over every thread's messages.
        conversation.LastMessageAt = message.SentAt;

        // Rule 131. A message brings the thread back for whichever side deleted it —
        // the recipient so the message is not lost in a thread they cannot see, and the
        // sender because they are plainly using it again.
        ConversationAccess.Reveal(conversation, conversation.SeekerId);
        ConversationAccess.Reveal(conversation, conversation.OwnerId);

        await _unitOfWork.SaveChangesAsync();

        // §4.2: the body is on the never-log list. What was sent, in which thread, by
        // whom — and not a character of what it said.
        _logger.LogInformation(
            "User {UserId} sent a message in conversation {ConversationId}",
            request.SenderId, conversation.Id);

        var recipientId = ConversationAccess.Other(conversation, request.SenderId);
        var recipient = conversation.SeekerId == recipientId
            ? conversation.Seeker
            : conversation.Owner;

        await DeliverAsync(conversation, message, recipientId, recipient, request.SenderId);

        // The sender's own copy. IsMine is the one field that differs between the two
        // views of the same row, which is exactly why it is not mapped.
        return Describe(message, conversation, viewerId: request.SenderId);
    }

    /// <summary>
    /// Rule 105: pushed while they are connected, emailed when they are not.
    /// </summary>
    private async Task DeliverAsync(
        Conversation conversation, Message message, int recipientId, AppUser recipient, int senderId)
    {
        if (await _realtime.IsOnlineAsync(recipientId))
        {
            await _realtime.MessageReceivedAsync(
                recipientId, Describe(message, conversation, viewerId: recipientId));

            return;
        }

        // The throttle is a counter rather than a get-then-set, so two messages landing
        // in the same instant cannot both read "nothing sent yet" and both send. Only
        // the call that sees 1 is the first one in the window.
        var sent = await _cache.IncrementAsync(
            CacheKeys.MessageNudge(conversation.Id, recipientId),
            CacheKeys.MessageNudgeWindow);

        if (sent > 1)
        {
            _logger.LogDebug(
                "Nudge for conversation {ConversationId} to user {UserId} suppressed by throttle",
                conversation.Id, recipientId);

            return;
        }

        var senderName = conversation.SeekerId == senderId
            ? conversation.Seeker.FullName
            : conversation.Owner.FullName;

        // The nudge says a message is waiting and never what it said. §10 calls it a
        // short nudge, and an email that quotes the message would put a private
        // conversation into an inbox the product does not control.
        await _email.SendMessageNudgeAsync(recipient, conversation.Property.Title, senderName);
    }

    private MessageDto Describe(Message message, Conversation conversation, int viewerId)
    {
        var dto = _mapper.Map<MessageDto>(message);

        dto.IsMine = message.SenderId == viewerId;

        // Mapped from the tracked graph rather than a reload: the sender is one of the
        // two people already attached to the conversation.
        dto.SenderDisplayName = conversation.SeekerId == message.SenderId
            ? conversation.Seeker.FullName
            : conversation.Owner.FullName;

        return dto;
    }
}
