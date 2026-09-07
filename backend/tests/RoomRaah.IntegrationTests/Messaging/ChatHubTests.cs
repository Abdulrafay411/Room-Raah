using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Messaging;

/// <summary>
/// The live half of messaging, against the real hub in the real host.
///
/// Long polling rather than WebSockets, on purpose: <c>TestServer</c> reaches the hub
/// over its own handler either way, and what these tests are about is the push, the
/// presence and rule 106 — none of which is a property of the transport. Choosing the
/// transport also removes the negotiate fallback, so a failure here is a failure in the
/// product rather than a transport that quietly downgraded.
/// </summary>
[Collection(ApiCollection.Name)]
public class ChatHubTests
{
    private readonly RoomRaahApiFactory _factory;

    public ChatHubTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private HubConnection Connect(string accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();
    }

    private async Task<(SeekerWorkspace Seeker, OwnerWorkspace Owner, int ConversationId)>
        ThreadAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, label);

        var conversation = await (await seeker.Client.PostJsonAsync(
                "/api/v1/conversations", new { PropertyId = listing.Id }))
            .ReadAsync<ConversationDto>();

        return (seeker, owner, conversation.Id);
    }

    private static Task SendAsync(HttpClient client, int conversationId, string body) =>
        client.PostJsonAsync($"/api/v1/conversations/{conversationId}/messages", new { Body = body })
            .ContinueWith(t => t.Result.EnsureSuccessStatusCode());

    /// <summary>
    /// Waits for something the server does after the request that caused it has already
    /// returned — a disconnect registering, a push arriving. Polled rather than slept
    /// on, so the test is as fast as the machine allows and still not a race.
    /// </summary>
    private static async Task<bool> EventuallyAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(50);
        }

        return condition();
    }

    [Fact]
    public async Task A_connected_recipient_is_pushed_the_message()
    {
        var (seeker, owner, conversationId) = await ThreadAsync("hub-push");

        await using var connection = Connect(owner.AccessToken);

        var received = new TaskCompletionSource<MessageDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<MessageDto>("MessageReceived", message => received.TrySetResult(message));

        await connection.StartAsync();

        await SendAsync(seeker.Client, conversationId, "Is the room still free?");

        var completed = await Task.WhenAny(received.Task, Task.Delay(10_000));

        completed.ShouldBe(received.Task, "the owner was connected and should have been pushed to");

        var pushed = await received.Task;

        pushed.Body.ShouldBe("Is the room still free?");
        pushed.ConversationId.ShouldBe(conversationId);

        // The recipient's own view of the row, not the sender's.
        pushed.IsMine.ShouldBeFalse();
    }

    /// <summary>
    /// Rule 105 from the presence side. A connected recipient is not emailed, and the
    /// same person a moment after they disconnect is — which is the whole behaviour the
    /// nudge exists for, and the only test that exercises both halves of it.
    /// </summary>
    [Fact]
    public async Task A_connected_recipient_is_not_emailed_and_a_disconnected_one_is()
    {
        var (seeker, owner, conversationId) = await ThreadAsync("hub-presence");

        var connection = Connect(owner.AccessToken);
        await connection.StartAsync();

        await SendAsync(seeker.Client, conversationId, "Are you there?");

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "MessageNudge").ShouldBe(0);

        await connection.StopAsync();
        await connection.DisposeAsync();

        // The disconnect is handled after StopAsync returns, so the assertion waits for
        // the server to have noticed rather than assuming it already has.
        await SendAsync(seeker.Client, conversationId, "Still there?");

        var nudged = await EventuallyAsync(() =>
            _factory.Emails.DecisionsFor(owner.Email)
                .Count(d => d.Decision == "MessageNudge") == 1);

        nudged.ShouldBeTrue("the owner had disconnected, so the message should have been emailed");
    }

    /// <summary>
    /// Rule 106. A third person holding a connection is not a participant in this
    /// thread, and there is no group in the product for them to end up in — every push
    /// is addressed to one user.
    /// </summary>
    [Fact]
    public async Task A_connected_stranger_is_not_pushed_somebody_else_s_message()
    {
        var (seeker, owner, conversationId) = await ThreadAsync("hub-stranger");
        var stranger = await SeekerWorkspace.CreateAsync(_factory, "hub-eavesdropper");

        await using var strangerConnection = Connect(stranger.AccessToken);
        await using var ownerConnection = Connect(owner.AccessToken);

        var strangerHeard = false;
        strangerConnection.On<MessageDto>("MessageReceived", _ => strangerHeard = true);

        var ownerHeard = new TaskCompletionSource<MessageDto>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ownerConnection.On<MessageDto>("MessageReceived", m => ownerHeard.TrySetResult(m));

        await strangerConnection.StartAsync();
        await ownerConnection.StartAsync();

        await SendAsync(seeker.Client, conversationId, "A private question.");

        // Waiting on the owner's push is what makes the stranger's silence meaningful:
        // by the time the message has been delivered to the person it was for, anyone
        // else who was going to receive it would have.
        var completed = await Task.WhenAny(ownerHeard.Task, Task.Delay(10_000));
        completed.ShouldBe(ownerHeard.Task);

        strangerHeard.ShouldBeFalse();
    }

    [Fact]
    public async Task An_unauthenticated_connection_is_refused()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, "hubs/chat"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await Should.ThrowAsync<Exception>(() => connection.StartAsync());
    }
}
