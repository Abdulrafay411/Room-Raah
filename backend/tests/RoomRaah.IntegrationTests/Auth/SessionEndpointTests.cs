using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class SessionEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public SessionEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"session-{Guid.NewGuid():N}@roomraah.local";

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken)
    {
        return _client.PostJsonAsync("/api/v1/auth/refresh", new { RefreshToken = refreshToken });
    }

    [Fact]
    public async Task Rule_9_refresh_rotates_the_token()
    {
        var session = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var response = await RefreshAsync(session.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rotated = await response.ReadAsync<AuthResponseDto>();
        rotated.RefreshToken.ShouldNotBe(session.RefreshToken);
        rotated.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rule_9_the_old_token_stops_working_once_it_has_been_rotated()
    {
        var session = await _client.RegisterAndVerifyAsync(_factory, NewEmail());
        (await RefreshAsync(session.RefreshToken)).EnsureSuccessStatusCode();

        var reuse = await RefreshAsync(session.RefreshToken);

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_401()
    {
        var response = await RefreshAsync("not-a-real-token");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_empty_refresh_token_is_400()
    {
        var response = await RefreshAsync("");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "RefreshToken");
    }

    [Fact]
    public async Task Rule_10_logout_revokes_the_token()
    {
        var session = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var client = _factory.CreateClient();
        client.Authorise(session.AccessToken);

        var logout = await client.PostJsonAsync(
            "/api/v1/auth/logout", new { RefreshToken = session.RefreshToken });

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterLogout = await RefreshAsync(session.RefreshToken);
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_without_a_token_is_401()
    {
        var session = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var response = await _client.PostJsonAsync(
            "/api/v1/auth/logout", new { RefreshToken = session.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task One_person_cannot_revoke_another_persons_session()
    {
        var victim = await _client.RegisterAndVerifyAsync(_factory, NewEmail());
        var attacker = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var client = _factory.CreateClient();
        client.Authorise(attacker.AccessToken);

        var response = await client.PostJsonAsync(
            "/api/v1/auth/logout", new { RefreshToken = victim.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var victimCanStillRefresh = await RefreshAsync(victim.RefreshToken);
        victimCanStillRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_account_id_sent_in_the_logout_body_is_ignored()
    {
        var victim = await _client.RegisterAndVerifyAsync(_factory, NewEmail());
        var attacker = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var client = _factory.CreateClient();
        client.Authorise(attacker.AccessToken);

        var response = await client.PostJsonAsync("/api/v1/auth/logout", new
        {
            RefreshToken = victim.RefreshToken,
            AppUserId = victim.User.Id
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RefreshAsync(victim.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_with_an_empty_token_is_400()
    {
        var session = await _client.RegisterAndVerifyAsync(_factory, NewEmail());

        var client = _factory.CreateClient();
        client.Authorise(session.AccessToken);

        var response = await client.PostJsonAsync("/api/v1/auth/logout", new { RefreshToken = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
