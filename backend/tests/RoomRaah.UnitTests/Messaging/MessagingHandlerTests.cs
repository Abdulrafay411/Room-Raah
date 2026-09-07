using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Messaging.Commands;
using RoomRaah.Application.Features.Messaging.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Messaging;

public class MessagingHandlerTests
{
    private const int OwnerId = 1;
    private const int SeekerId = 2;
    private const int Stranger = 99;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IConversationRepository _conversations = Substitute.For<IConversationRepository>();
    private readonly IMessageRepository _messages = Substitute.For<IMessageRepository>();
    private readonly IRealtimeNotifier _realtime = Substitute.For<IRealtimeNotifier>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    private StartConversationCommandHandler Start() =>
        new(_properties, _conversations, _messages, _unitOfWork, _mapper,
            NullLogger<StartConversationCommandHandler>.Instance);

    private SendMessageCommandHandler Send() =>
        new(_conversations, _messages, _realtime, _cache, _email, _unitOfWork, _mapper,
            NullLogger<SendMessageCommandHandler>.Instance);

    private MarkConversationReadCommandHandler MarkRead() =>
        new(_conversations, _messages);

    private GetConversationsQueryHandler List() =>
        new(_conversations, _messages, _mapper);

    private GetMessagesQueryHandler Thread() =>
        new(_conversations, _messages, _mapper);

    // ------------------------------------------------------------------- start

    [Fact]
    public async Task A_conversation_is_opened_on_a_published_listing()
    {
        var property = TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published);
        _properties.GetPublishedWithOwnerAsync(1).Returns(property);
        _conversations.GetForSeekerAndPropertyAsync(SeekerId, 1).Returns((Conversation?)null);
        _conversations.GetByIdAsync(Arg.Any<int>())
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));

        var result = await Start().Handle(
            new StartConversationCommand { SeekerId = SeekerId, PropertyId = 1 }, default);

        result.WasCreated.ShouldBeTrue();

        await _conversations.Received(1).CreateAsync(Arg.Is<Conversation>(c =>
            c.SeekerId == SeekerId && c.OwnerId == OwnerId && c.PropertyId == 1));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 100. §5 named no endpoint that opens a conversation, and rule 43 named a
    /// uniqueness constraint — which together would have made every second call an
    /// error for doing the ordinary thing.
    /// </summary>
    [Fact]
    public async Task Opening_a_conversation_twice_returns_the_first_one()
    {
        var existing = TestData.Conversation(id: 7, seekerId: SeekerId, ownerId: OwnerId);
        _properties.GetPublishedWithOwnerAsync(1)
            .Returns(TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));
        _conversations.GetForSeekerAndPropertyAsync(SeekerId, 1).Returns(existing);
        _messages.GetUnreadCountsAsync(SeekerId, Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, int> { [7] = 3 });

        var result = await Start().Handle(
            new StartConversationCommand { SeekerId = SeekerId, PropertyId = 1 }, default);

        result.WasCreated.ShouldBeFalse();
        result.Conversation.Id.ShouldBe(7);

        await _conversations.DidNotReceive().CreateAsync(Arg.Any<Conversation>());
    }

    /// <summary>
    /// The thread handed back is not an empty one. Answering zero here would hide
    /// messages the seeker has never read behind a badge that says there are none.
    /// </summary>
    [Fact]
    public async Task A_returned_conversation_still_reports_what_is_unread()
    {
        var existing = TestData.Conversation(id: 7, seekerId: SeekerId, ownerId: OwnerId);
        _properties.GetPublishedWithOwnerAsync(1)
            .Returns(TestData.ReviewableProperty(1, OwnerId, PropertyStatus.Published));
        _conversations.GetForSeekerAndPropertyAsync(SeekerId, 1).Returns(existing);
        _messages.GetUnreadCountsAsync(SeekerId, Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, int> { [7] = 3 });

        var result = await Start().Handle(
            new StartConversationCommand { SeekerId = SeekerId, PropertyId = 1 }, default);

        result.Conversation.UnreadCount.ShouldBe(3);
    }

    /// <summary>Rule 100: a listing that is not published cannot be written about.</summary>
    [Fact]
    public async Task A_listing_that_is_not_published_cannot_be_written_about()
    {
        _properties.GetPublishedWithOwnerAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => Start().Handle(
            new StartConversationCommand { SeekerId = SeekerId, PropertyId = 1 }, default));
    }

    // -------------------------------------------------------------------- send

    [Fact]
    public async Task A_message_is_stored_and_stamps_the_conversation()
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId,
            lastMessageAt: DateTime.UtcNow.AddDays(-3));
        _conversations.GetByIdAsync(1).Returns(conversation);
        _realtime.IsOnlineAsync(OwnerId).Returns(true);

        var before = conversation.LastMessageAt;

        var dto = await Send().Handle(new SendMessageCommand
        {
            ConversationId = 1,
            SenderId = SeekerId,
            Body = "Is the room still free?"
        }, default);

        dto.IsMine.ShouldBeTrue();
        dto.Body.ShouldBe("Is the room still free?");

        // Rule 103.
        conversation.LastMessageAt.ShouldBeGreaterThan(before);

        await _messages.Received(1).CreateAsync(Arg.Is<Message>(m =>
            m.SenderId == SeekerId && m.Body == "Is the room still free?"));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>Rule 102, on the write path.</summary>
    [Fact]
    public async Task A_stranger_cannot_post_into_a_conversation()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = Stranger, Body = "Hello" },
            default));

        await _messages.DidNotReceive().CreateAsync(Arg.Any<Message>());
    }

    /// <summary>
    /// Rule 105. A connected recipient is pushed to, and no email goes out — the whole
    /// point of the nudge is that it reaches somebody who is not looking at the screen.
    /// </summary>
    [Fact]
    public async Task A_connected_recipient_is_pushed_to_and_not_emailed()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _realtime.IsOnlineAsync(OwnerId).Returns(true);

        await Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = SeekerId, Body = "Hello" },
            default);

        await _realtime.Received(1).MessageReceivedAsync(OwnerId, Arg.Any<MessageDto>());
        await _email.DidNotReceive().SendMessageNudgeAsync(
            Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The pushed copy is the recipient's view, not the sender's. Sending the sender's
    /// own DTO would light up the other person's screen with a message marked as theirs.
    /// </summary>
    [Fact]
    public async Task The_pushed_copy_belongs_to_the_person_receiving_it()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _realtime.IsOnlineAsync(OwnerId).Returns(true);

        await Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = SeekerId, Body = "Hello" },
            default);

        await _realtime.Received(1).MessageReceivedAsync(
            OwnerId, Arg.Is<MessageDto>(m => !m.IsMine));
    }

    /// <summary>Rule 105, the other half.</summary>
    [Fact]
    public async Task A_recipient_who_is_not_connected_is_emailed()
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);
        _conversations.GetByIdAsync(1).Returns(conversation);
        _realtime.IsOnlineAsync(OwnerId).Returns(false);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(1L);

        await Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = SeekerId, Body = "Hello" },
            default);

        await _email.Received(1).SendMessageNudgeAsync(
            conversation.Owner, conversation.Property.Title, conversation.Seeker.FullName);
        await _realtime.DidNotReceive().MessageReceivedAsync(
            Arg.Any<int>(), Arg.Any<MessageDto>());
    }

    /// <summary>
    /// Rule 105's limit. Without it, twenty messages in a minute are twenty emails, and
    /// the nudge becomes the thing people mute.
    /// </summary>
    [Fact]
    public async Task A_second_nudge_inside_the_hour_is_not_sent()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _realtime.IsOnlineAsync(OwnerId).Returns(false);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(2L);

        await Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = SeekerId, Body = "Hello" },
            default);

        await _email.DidNotReceive().SendMessageNudgeAsync(
            Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The throttle names the recipient as well as the thread, because §10's limit is
    /// about an inbox. A key on the conversation alone would let one person's nudge
    /// silence the reply coming back the other way.
    /// </summary>
    [Fact]
    public async Task The_nudge_throttle_is_keyed_on_the_thread_and_the_recipient()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(id: 5, seekerId: SeekerId, ownerId: OwnerId));
        _realtime.IsOnlineAsync(OwnerId).Returns(false);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(1L);

        await Send().Handle(
            new SendMessageCommand { ConversationId = 1, SenderId = SeekerId, Body = "Hello" },
            default);

        await _cache.Received(1).IncrementAsync(
            CacheKeys.MessageNudge(5, OwnerId), CacheKeys.MessageNudgeWindow);
    }

    // ---------------------------------------------------------------- markread

    [Fact]
    public async Task Marking_a_conversation_read_reports_what_it_marked()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _messages.MarkReadAsync(1, SeekerId, Arg.Any<DateTime>()).Returns(4);

        var result = await MarkRead().Handle(
            new MarkConversationReadCommand { ConversationId = 1, ReaderId = SeekerId }, default);

        result.MarkedCount.ShouldBe(4);
        result.ConversationId.ShouldBe(1);
    }

    /// <summary>
    /// Rule 104. Opening a thread twice is what a client does, not a mistake it makes,
    /// so a second call marks nothing and succeeds.
    /// </summary>
    [Fact]
    public async Task Marking_a_conversation_with_nothing_unread_is_not_a_refusal()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _messages.MarkReadAsync(1, SeekerId, Arg.Any<DateTime>()).Returns(0);

        var result = await MarkRead().Handle(
            new MarkConversationReadCommand { ConversationId = 1, ReaderId = SeekerId }, default);

        result.MarkedCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_stranger_cannot_mark_a_conversation_read()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => MarkRead().Handle(
            new MarkConversationReadCommand { ConversationId = 1, ReaderId = Stranger }, default));
    }

    // ------------------------------------------------------------------- reads

    /// <summary>Rule 101, from both sides of the same thread.</summary>
    [Theory]
    [InlineData(SeekerId, "Bilal Ahmed")]
    [InlineData(OwnerId, "Ayesha Khan")]
    public async Task An_inbox_names_the_other_participant(int viewerId, string expected)
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);
        conversation.Seeker.FullName = "Ayesha Khan";
        conversation.Owner.FullName = "Bilal Ahmed";

        _conversations.GetForUserAsync(viewerId, 1, 12).Returns([conversation]);
        _conversations.CountForUserAsync(viewerId).Returns(1);
        _messages.GetUnreadCountsAsync(viewerId, Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, int>());

        var page = await List().Handle(
            new GetConversationsQuery { UserId = viewerId }, default);

        page.Items.Single().OtherParticipantDisplayName.ShouldBe(expected);
    }

    [Fact]
    public async Task An_inbox_carries_the_unread_count_for_the_caller()
    {
        var conversation = TestData.Conversation(id: 5, seekerId: SeekerId, ownerId: OwnerId);
        _conversations.GetForUserAsync(SeekerId, 1, 12).Returns([conversation]);
        _conversations.CountForUserAsync(SeekerId).Returns(1);
        _messages.GetUnreadCountsAsync(SeekerId, Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, int> { [5] = 2 });

        var page = await List().Handle(new GetConversationsQuery { UserId = SeekerId }, default);

        page.Items.Single().UnreadCount.ShouldBe(2);
    }

    /// <summary>A thread nobody has written in reports zero rather than falling over.</summary>
    [Fact]
    public async Task A_thread_with_nothing_unread_reports_zero()
    {
        _conversations.GetForUserAsync(SeekerId, 1, 12)
            .Returns([TestData.Conversation(id: 5, seekerId: SeekerId, ownerId: OwnerId)]);
        _conversations.CountForUserAsync(SeekerId).Returns(1);
        _messages.GetUnreadCountsAsync(SeekerId, Arg.Any<IReadOnlyCollection<int>>())
            .Returns(new Dictionary<int, int>());

        var page = await List().Handle(new GetConversationsQuery { UserId = SeekerId }, default);

        page.Items.Single().UnreadCount.ShouldBe(0);
    }

    /// <summary>Rule 102, on the read path — asked before a single message is loaded.</summary>
    [Fact]
    public async Task A_stranger_cannot_read_a_thread()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => Thread().Handle(
            new GetMessagesQuery { ConversationId = 1, UserId = Stranger }, default));

        await _messages.DidNotReceive().GetForConversationAsync(
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task A_thread_marks_which_messages_belong_to_the_caller()
    {
        _conversations.GetByIdAsync(1)
            .Returns(TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId));
        _messages.GetForConversationAsync(1, 1, 30).Returns([
            TestData.Message(id: 1, senderId: SeekerId, body: "Is it free?"),
            TestData.Message(id: 2, senderId: OwnerId, body: "Yes.")
        ]);
        _messages.CountForConversationAsync(1).Returns(2);

        var page = await Thread().Handle(
            new GetMessagesQuery { ConversationId = 1, UserId = SeekerId }, default);

        page.Items[0].IsMine.ShouldBeTrue();
        page.Items[1].IsMine.ShouldBeFalse();
    }

    [Fact]
    public async Task A_thread_that_does_not_exist_is_a_404()
    {
        _conversations.GetByIdAsync(1).Returns((Conversation?)null);

        await Should.ThrowAsync<NotFoundException>(() => Thread().Handle(
            new GetMessagesQuery { ConversationId = 1, UserId = SeekerId }, default));
    }
}
