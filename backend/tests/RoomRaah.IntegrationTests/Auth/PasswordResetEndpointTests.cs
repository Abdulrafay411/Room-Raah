using System.Net;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class PasswordResetEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"reset-{Guid.NewGuid():N}@roomraah.local";

    private Task<HttpResponseMessage> ForgotAsync(string email)
    {
        return _client.PostJsonAsync("/api/v1/auth/forgot-password", new { Email = email });
    }

    private Task<HttpResponseMessage> ResetAsync(string email, string token, string password)
    {
        return _client.PostJsonAsync("/api/v1/auth/reset-password", new
        {
            Email = email,
            Token = token,
            NewPassword = password
        });
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        return _client.PostJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
    }

    [Fact]
    public async Task Happy_path_emails_a_link_and_the_new_password_works()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);

        (await ForgotAsync(email)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var token = _factory.Emails.LatestSecretFor(email, OtpPurpose.ResetPassword);

        (await ResetAsync(email, token, "Karachi2026")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await LoginAsync(email, "Karachi2026")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(email, "Password1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rule_12_a_reset_link_works_only_once()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);
        await ForgotAsync(email);

        var token = _factory.Emails.LatestSecretFor(email, OtpPurpose.ResetPassword);
        (await ResetAsync(email, token, "Karachi2026")).EnsureSuccessStatusCode();

        var second = await ResetAsync(email, token, "Lahore2026x");

        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await LoginAsync(email, "Karachi2026")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resetting_a_password_ends_every_existing_session()
    {
        var email = NewEmail();
        var session = await _client.RegisterAndVerifyAsync(_factory, email);
        await ForgotAsync(email);

        var token = _factory.Emails.LatestSecretFor(email, OtpPurpose.ResetPassword);
        (await ResetAsync(email, token, "Karachi2026")).EnsureSuccessStatusCode();

        var refresh = await _client.PostJsonAsync(
            "/api/v1/auth/refresh", new { RefreshToken = session.RefreshToken });

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_email_still_returns_204_so_accounts_cannot_be_enumerated()
    {
        var response = await ForgotAsync("nobody-at-all@roomraah.local");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _factory.Emails.CountFor("nobody-at-all@roomraah.local").ShouldBe(0);
    }

    [Fact]
    public async Task A_malformed_email_is_400()
    {
        var response = await ForgotAsync("not-an-email");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "Email");
    }

    [Fact]
    public async Task A_forged_token_is_422()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);
        await ForgotAsync(email);

        var response = await ResetAsync(email, "a-token-that-was-never-issued", "Karachi2026");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await LoginAsync(email, "Password1")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rule_2_the_new_password_must_obey_the_password_rules()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);
        await ForgotAsync(email);

        var token = _factory.Emails.LatestSecretFor(email, OtpPurpose.ResetPassword);

        var response = await ResetAsync(email, token, "weak");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "NewPassword");
    }

    [Fact]
    public async Task An_unknown_account_cannot_be_distinguished_from_a_bad_token()
    {
        var unknown = await ResetAsync("nobody@roomraah.local", "some-token", "Karachi2026");

        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);
        var badToken = await ResetAsync(email, "some-token", "Karachi2026");

        unknown.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        badToken.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await unknown.ReadErrorAsync()).Error.ShouldBe((await badToken.ReadErrorAsync()).Error);
    }
}
