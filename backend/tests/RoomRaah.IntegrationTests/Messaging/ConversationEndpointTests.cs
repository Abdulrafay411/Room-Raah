using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Messaging;

[Collection(ApiCollection.Name)]
public class ConversationEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public ConversationEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(SeekerWorkspace Seeker, OwnerWorkspace Owner, OwnerPropertyDto Listing)>
        PublishedAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, label);

        return (seeker, owner, listing);
    }

    private static async Task<ConversationDto> StartAsync(SeekerWorkspace seeker, int propertyId)
    {
        var response = await seeker.Client.PostJsonAsync(
            "/api/v1/conversations", new { PropertyId = propertyId });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await response.ReadAsync<ConversationDto>();
    }

    private static async Task<MessageDto> SendAsync(HttpClient client, int conversationId, string body)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages", new { Body = body });

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<MessageDto>();
    }

    // ------------------------------------------------------------ the journey

    [Fact]
    public async Task A_seeker_writes_to_an_owner_and_the_owner_writes_back()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-journey");

        var conversation = await StartAsync(seeker, listing.Id);

        conversation.PropertyTitle.ShouldBe(listing.Title);
        conversation.UnreadCount.ShouldBe(0);

        await SendAsync(seeker.Client, conversation.Id, "Is the room still free from the 1st?");
        await SendAsync(owner.Client, conversation.Id, "Yes, one bed is free from the 1st.");

        var thread = await (await seeker.Client.GetAsync(
                $"/api/v1/conversations/{conversation.Id}/messages"))
            .ReadAsync<PagedResult<MessageDto>>();

        thread.Items.Count.ShouldBe(2);

        // Oldest first, and each side knows which messages are its own.
        thread.Items[0].IsMine.ShouldBeTrue();
        thread.Items[1].IsMine.ShouldBeFalse();
    }

    /// <summary>
    /// Rule 100. §5 listed three endpoints that act on a conversation and none that
    /// opens one, and rule 43 made it unique — so the second call had to be decided one
    /// way or the other. It is not an error.
    /// </summary>
    [Fact]
    public async Task Opening_the_same_conversation_twice_returns_the_first_one()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-twice");

        var first = await StartAsync(seeker, listing.Id);

        var again = await seeker.Client.PostJsonAsync(
            "/api/v1/conversations", new { PropertyId = listing.Id });

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.ReadAsync<ConversationDto>()).Id.ShouldBe(first.Id);
    }

    /// <summary>Rule 100: a listing nobody can see is a listing nobody can write about.</summary>
    [Fact]
    public async Task A_conversation_cannot_be_opened_on_a_listing_that_is_not_published()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "chat-draft");
        var draft = await owner.CreateDraftAsync();
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "chat-draft");

        var response = await seeker.Client.PostJsonAsync(
            "/api/v1/conversations", new { PropertyId = draft.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --------------------------------------------------------------- privacy

    /// <summary>
    /// Rules 33 and 93 in the one place two people talk directly. Asserted against the
    /// raw body rather than the DTO, because a DTO that is right today is only evidence
    /// about the DTO — the body is what the other person actually receives.
    /// </summary>
    [Fact]
    public async Task Neither_participant_is_given_the_other_s_contact_details()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-privacy");

        var conversation = await StartAsync(seeker, listing.Id);
        await SendAsync(seeker.Client, conversation.Id, "Is it still free?");
        await SendAsync(owner.Client, conversation.Id, "Yes it is.");

        var seekerInbox = await (await seeker.Client.GetAsync("/api/v1/conversations"))
            .Content.ReadAsStringAsync();
        var ownerInbox = await (await owner.Client.GetAsync("/api/v1/conversations"))
            .Content.ReadAsStringAsync();
        var seekerThread = await (await seeker.Client.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages")).Content.ReadAsStringAsync();
        var ownerThread = await (await owner.Client.GetAsync(
            $"/api/v1/conversations/{conversation.Id}/messages")).Content.ReadAsStringAsync();

        foreach (var body in new[] { seekerInbox, seekerThread })
        {
            body.ShouldNotContain(owner.Email);
            body.ShouldNotContain("+92300");
        }

        foreach (var body in new[] { ownerInbox, ownerThread })
        {
            body.ShouldNotContain(seeker.Email);
            body.ShouldNotContain("+92300");
        }
    }

    // ------------------------------------------------------------ rule 102

    [Fact]
    public async Task A_stranger_can_neither_read_a_thread_nor_post_into_it()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-stranger");
        var conversation = await StartAsync(seeker, listing.Id);

        var stranger = await SeekerWorkspace.CreateAsync(_factory, "chat-outsider");

        (await stranger.Client.GetAsync($"/api/v1/conversations/{conversation.Id}/messages"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await stranger.Client.PostJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages", new { Body = "Hello" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await stranger.Client.PatchJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/read", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A stranger's inbox is theirs alone. Rule 101 filters in the repository, so this
    /// is the assertion that the filter is actually there.
    /// </summary>
    [Fact]
    public async Task An_inbox_holds_only_the_caller_s_own_conversations()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-inbox");
        var conversation = await StartAsync(seeker, listing.Id);

        var stranger = await SeekerWorkspace.CreateAsync(_factory, "chat-inbox-other");

        var inbox = await (await stranger.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        inbox.Items.Select(c => c.Id).ShouldNotContain(conversation.Id);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/conversations"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ----------------------------------------------------------- unread and read

    [Fact]
    public async Task An_unread_message_shows_in_the_recipient_s_inbox_and_clears_when_read()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-unread");

        var conversation = await StartAsync(seeker, listing.Id);
        await SendAsync(owner.Client, conversation.Id, "The room is ready to see.");
        await SendAsync(owner.Client, conversation.Id, "Come any evening this week.");

        var before = await (await seeker.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        before.Items.Single(c => c.Id == conversation.Id).UnreadCount.ShouldBe(2);

        var marked = await (await seeker.Client.PatchJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/read", new { }))
            .ReadAsync<MarkReadResultDto>();

        marked.MarkedCount.ShouldBe(2);

        var after = await (await seeker.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        after.Items.Single(c => c.Id == conversation.Id).UnreadCount.ShouldBe(0);
    }

    /// <summary>
    /// Rule 104. Marking a thread that holds nothing unread is what a client does every
    /// time somebody opens a conversation twice, and refusing it would be refusing the
    /// ordinary case.
    /// </summary>
    [Fact]
    public async Task Marking_a_conversation_with_nothing_unread_succeeds_with_zero()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-read-twice");
        var conversation = await StartAsync(seeker, listing.Id);

        var response = await seeker.Client.PatchJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/read", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<MarkReadResultDto>()).MarkedCount.ShouldBe(0);
    }

    /// <summary>
    /// A sender does not mark their own messages read, or every thread would report
    /// zero unread the moment anybody wrote in it.
    /// </summary>
    [Fact]
    public async Task Reading_a_thread_does_not_mark_the_caller_s_own_messages()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-own");

        var conversation = await StartAsync(seeker, listing.Id);
        await SendAsync(seeker.Client, conversation.Id, "Is it free?");

        var marked = await (await seeker.Client.PatchJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/read", new { }))
            .ReadAsync<MarkReadResultDto>();

        marked.MarkedCount.ShouldBe(0);

        var ownerInbox = await (await owner.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        ownerInbox.Items.Single(c => c.Id == conversation.Id).UnreadCount.ShouldBe(1);
    }

    /// <summary>
    /// An unread count is what the caller has not read, not what is unread in the
    /// thread.
    ///
    /// This test exists because a probe removed the sender filter from the count and
    /// nothing failed: every other test here has one side writing and the other side
    /// counting, so a caller's own messages were never in the thread they were
    /// counting. The case that matters is the ordinary one — both people have written,
    /// and neither has read the other yet.
    /// </summary>
    [Fact]
    public async Task An_unread_count_does_not_include_the_caller_s_own_messages()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-unread-own");

        var conversation = await StartAsync(seeker, listing.Id);

        await SendAsync(seeker.Client, conversation.Id, "Is the room still free?");
        await SendAsync(owner.Client, conversation.Id, "Yes, come and see it.");

        // Two messages in the thread and exactly one of them is unread by each side.
        var seekerInbox = await (await seeker.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();
        var ownerInbox = await (await owner.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        seekerInbox.Items.Single(c => c.Id == conversation.Id).UnreadCount.ShouldBe(1);
        ownerInbox.Items.Single(c => c.Id == conversation.Id).UnreadCount.ShouldBe(1);
    }

    // ------------------------------------------------------------- ordering

    /// <summary>Rule 103: the inbox follows the most recent message, not the opening date.</summary>
    [Fact]
    public async Task The_inbox_is_ordered_by_the_most_recent_message()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "chat-order");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var first = await owner.CreatePublishedAsync(admin);
        var second = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "chat-order");

        var older = await StartAsync(seeker, first.Id);
        var newer = await StartAsync(seeker, second.Id);

        // Written into the older thread, which should put it back on top.
        await SendAsync(seeker.Client, older.Id, "Still interested in this one.");

        var inbox = await (await seeker.Client.GetAsync("/api/v1/conversations"))
            .ReadAsync<PagedResult<ConversationDto>>();

        var ids = inbox.Items.Select(c => c.Id).ToList();

        ids.IndexOf(older.Id).ShouldBeLessThan(ids.IndexOf(newer.Id));
    }

    // ------------------------------------------------------------ validation

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_message_is_refused(string body)
    {
        var (seeker, _, listing) = await PublishedAsync($"chat-empty-{body.Length}");
        var conversation = await StartAsync(seeker, listing.Id);

        var response = await seeker.Client.PostJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages", new { Body = body });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.ReadErrorAsync();
        error.Errors.ShouldNotBeNull();
        error.Errors!.ShouldContain(e => e.Field == "Body");
    }

    [Fact]
    public async Task A_message_past_two_thousand_characters_is_refused()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-long");
        var conversation = await StartAsync(seeker, listing.Id);

        var response = await seeker.Client.PostJsonAsync(
            $"/api/v1/conversations/{conversation.Id}/messages",
            new { Body = new string('a', 2001) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_conversation_that_does_not_exist_is_a_404()
    {
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "chat-missing");

        (await seeker.Client.GetAsync("/api/v1/conversations/987654/messages"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------- email

    /// <summary>
    /// Rule 105. Nobody is connected in this test, so the message must produce exactly
    /// one nudge — and a second message inside the hour must produce none.
    /// </summary>
    [Fact]
    public async Task An_offline_recipient_is_nudged_once_and_not_twice()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-nudge");
        var conversation = await StartAsync(seeker, listing.Id);

        await SendAsync(seeker.Client, conversation.Id, "Is the room still free?");
        await SendAsync(seeker.Client, conversation.Id, "Sorry, one more thing.");
        await SendAsync(seeker.Client, conversation.Id, "Is the mess included?");

        var nudges = _factory.Emails.DecisionsFor(owner.Email)
            .Where(d => d.Decision == "MessageNudge")
            .ToArray();

        nudges.Length.ShouldBe(1);

        // What the nudge carries: the listing, and the sender's display name. Not the
        // message, which is the whole reason it stays on the site.
        nudges[0].Subject.ShouldBe(listing.Title);
        nudges[0].Note.ShouldNotBeNull();
        nudges[0].Note!.ShouldNotContain("mess");
    }

    /// <summary>
    /// The throttle names the recipient, so a reply going the other way is nudged on
    /// its own account rather than silenced by the message it answers.
    /// </summary>
    [Fact]
    public async Task A_reply_the_other_way_is_nudged_on_its_own_account()
    {
        var (seeker, owner, listing) = await PublishedAsync("chat-nudge-both");
        var conversation = await StartAsync(seeker, listing.Id);

        await SendAsync(seeker.Client, conversation.Id, "Is the room still free?");
        await SendAsync(owner.Client, conversation.Id, "Yes, come and see it.");

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "MessageNudge").ShouldBe(1);

        _factory.Emails.DecisionsFor(seeker.Email)
            .Count(d => d.Decision == "MessageNudge").ShouldBe(1);
    }

    // ----------------------------------------------------------------- logs

    /// <summary>
    /// §4.2 puts a message body on the never-log list. The log says a message was sent,
    /// in which thread, by whom — a private conversation does not belong in a file a
    /// wider set of people can read than can read the conversation.
    /// </summary>
    [Fact]
    public async Task A_message_body_is_never_written_to_a_log()
    {
        var (seeker, _, listing) = await PublishedAsync("chat-log");
        var conversation = await StartAsync(seeker, listing.Id);

        const string secret = "Zebra pomegranate lighthouse, my budget is 14500.";

        await SendAsync(seeker.Client, conversation.Id, secret);

        _factory.Logs.Mentioning("pomegranate").ShouldBeEmpty();
        _factory.Logs.Mentioning($"sent a message in conversation {conversation.Id}")
            .ShouldNotBeEmpty();
    }
}
