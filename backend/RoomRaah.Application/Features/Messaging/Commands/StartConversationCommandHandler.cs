using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Application.Messaging;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class StartConversationCommandHandler
    : IRequestHandler<StartConversationCommand, StartConversationResult>
{
    private readonly IPropertyRepository _properties;
    private readonly IConversationRepository _conversations;
    private readonly IMessageRepository _messages;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<StartConversationCommandHandler> _logger;

    public StartConversationCommandHandler(
        IPropertyRepository properties,
        IConversationRepository conversations,
        IMessageRepository messages,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<StartConversationCommandHandler> logger)
    {
        _properties = properties;
        _conversations = conversations;
        _messages = messages;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<StartConversationResult> Handle(
        StartConversationCommand request, CancellationToken cancellationToken)
    {
        // Rule 100. A conversation names a listing the way a stranger names one, so a
        // listing that is not published cannot be written about at all.
        var property = await _properties.GetPublishedWithOwnerAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        // Rule 43 is what makes this safe to call twice. A caller that has lost track
        // of a conversation id is not making a mistake, and refusing it would only mean
        // every client had to ask whether one existed before it could open one.
        var existing = await _conversations.GetForSeekerAndPropertyAsync(
            request.SeekerId, property.Id);

        if (existing is not null)
        {
            // Rule 131. A seeker who deleted this thread and asks for it again gets it back
            // in their inbox, still without what they deleted.
            if (existing.HiddenFromSeeker)
            {
                ConversationAccess.Reveal(existing, request.SeekerId);
                await _unitOfWork.SaveChangesAsync();
            }

            // A thread handed back is not a thread nobody has written in. Counting here
            // rather than answering zero is the difference between a client that shows
            // an unread badge and one that hides messages the seeker has not read.
            var unread = await _messages.GetUnreadCountsAsync(
                request.SeekerId, new[] { existing.Id });

            return new StartConversationResult(
                Describe(existing, request.SeekerId, unread.GetValueOrDefault(existing.Id)),
                false);
        }

        var conversation = new Conversation
        {
            PropertyId = property.Id,
            SeekerId = request.SeekerId,

            // Copied rather than reached through the listing. A listing's owner never
            // changes, and holding it here makes rule 102 one comparison on every
            // single message instead of a join.
            OwnerId = property.OwnerId
        };

        await _conversations.CreateAsync(conversation);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} opened conversation {ConversationId} on property {PropertyId}",
            request.SeekerId, conversation.Id, property.Id);

        // Reloaded so the response is built from the same graph the list endpoint uses,
        // rather than the half-populated one the writer happens to be holding.
        var saved = await _conversations.GetByIdAsync(conversation.Id);

        // A thread that did not exist a moment ago holds nothing to be unread.
        return new StartConversationResult(Describe(saved!, request.SeekerId, 0), true);
    }

    private ConversationDto Describe(Conversation conversation, int viewerId, int unreadCount)
    {
        var dto = _mapper.Map<ConversationDto>(conversation);

        dto.OtherParticipantDisplayName = ConversationAccess.OtherDisplayName(conversation, viewerId);
        dto.UnreadCount = unreadCount;

        return dto;
    }
}
