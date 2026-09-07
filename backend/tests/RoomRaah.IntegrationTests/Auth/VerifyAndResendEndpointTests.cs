using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class VerifyAndResendEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public VerifyAndResendEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"otp-{Guid.NewGuid():N}@roomraah.local";

    private async Task<string> RegisterAsync(string email)
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        });

        response.EnsureSuccessStatusCode();
        return _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);
    }

    [Fact]
    public async Task Happy_path_verifies_the_account_and_returns_a_verified_session()
    {
        var email = NewEmail();
        var code = await RegisterAsync(email);

        var response = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.ReadAsync<AuthResponseDto>();
        session.AccessToken.ShouldNotBeNullOrWhiteSpace();
        session.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        session.User.IsEmailVerified.ShouldBeTrue();
    }

    [Fact]
    public async Task Rule_6_a_code_cannot_be_used_twice()
    {
        var email = NewEmail();
        var code = await RegisterAsync(email);

        (await _client.PostJsonAsync("/api/v1/auth/verify-otp", new { Email = email, Code = code }))
            .EnsureSuccessStatusCode();

        var second = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code });

        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_6_a_wrong_code_is_refused_with_422()
    {
        var email = NewEmail();
        var code = await RegisterAsync(email);
        var wrong = code == "000000" ? "111111" : "000000";

        var response = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = wrong });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_6_the_code_is_burned_after_five_wrong_attempts()
    {
        var email = NewEmail();
        var code = await RegisterAsync(email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _client.PostJsonAsync("/api/v1/auth/verify-otp", new { Email = email, Code = wrong });
        }

        var withTheRightCode = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code });

        withTheRightCode.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await withTheRightCode.ReadErrorAsync()).Error.ShouldContain("too many times");
    }

    [Fact]
    public async Task A_malformed_code_is_refused_with_400()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var response = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = "abc" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "Code");
    }

    [Fact]
    public async Task An_unknown_email_is_404()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/verify-otp", new
        {
            Email = "nobody@roomraah.local",
            Code = "123456"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resending_replaces_the_previous_code()
    {
        var email = NewEmail();
        var first = await RegisterAsync(email);

        var resend = await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });
        resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var second = _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);
        second.ShouldNotBe(first);

        var withTheOldCode = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = first });
        withTheOldCode.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var withTheNewCode = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = second });
        withTheNewCode.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resending_to_an_already_verified_account_is_422()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);

        var response = await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_7_a_fourth_code_in_the_window_is_refused_with_429_and_a_retry_after()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });
        await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });

        var fourth = await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });

        fourth.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        fourth.Headers.RetryAfter.ShouldNotBeNull();

        var error = await fourth.ReadErrorAsync();
        error.RetryAfterSeconds.ShouldBe(600);
        _factory.Emails.CountFor(email).ShouldBe(3);
    }
}
