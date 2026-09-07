using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.DTOs;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class LoginEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public LoginEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"login-{Guid.NewGuid():N}@roomraah.local";

    private Task<HttpResponseMessage> LoginAsync(string email, string password = "Password1")
    {
        return _client.PostJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
    }

    [Fact]
    public async Task Happy_path_returns_both_tokens_and_the_account()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);

        var response = await LoginAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.ReadAsync<AuthResponseDto>();
        session.AccessToken.ShouldNotBeNullOrWhiteSpace();
        session.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        session.ExpiresInSeconds.ShouldBe(1800);
        session.User.Email.ShouldBe(email);
    }

    [Fact]
    public async Task The_seeded_accounts_can_log_in()
    {
        var response = await LoginAsync("admin@roomraah.local", DataSeeder.SeedPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<AuthResponseDto>()).User.Role.ShouldBe("Admin");
    }

    [Fact]
    public async Task The_access_token_carries_the_role_and_the_verified_flag()
    {
        var email = NewEmail();
        var session = await _client.RegisterAndVerifyAsync(_factory, email, "Owner");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);

        token.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Owner");
        token.Claims.First(c => c.Type == "email_verified").Value.ShouldBe("true");
    }

    [Fact]
    public async Task An_unknown_email_is_401()
    {
        var response = await LoginAsync("nobody@roomraah.local");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Error.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task A_wrong_password_is_401_with_the_same_message()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);

        var response = await LoginAsync(email, "Wrong12345");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadErrorAsync()).Error.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task An_empty_password_is_400()
    {
        var response = await LoginAsync(NewEmail(), "");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "Password");
    }

    [Fact]
    public async Task Rule_5_an_unverified_account_can_still_log_in()
    {
        var email = NewEmail();
        (await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Unverified Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        })).EnsureSuccessStatusCode();

        var response = await LoginAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var session = await response.ReadAsync<AuthResponseDto>();
        session.User.IsEmailVerified.ShouldBeFalse();

        new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken)
            .Claims.First(c => c.Type == "email_verified").Value.ShouldBe("false");
    }

    [Fact]
    public async Task Rule_11_a_deactivated_account_is_refused_with_422()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);
        await DeactivateAsync(email);

        var response = await LoginAsync(email);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("deactivated");
    }

    [Fact]
    public async Task The_login_is_not_case_sensitive_on_the_email()
    {
        var email = NewEmail();
        await _client.RegisterAndVerifyAsync(_factory, email);

        var response = await LoginAsync(email.ToUpperInvariant());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task DeactivateAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        var user = await context.AppUsers
            .IgnoreQueryFilters()
            .FirstAsync(u => u.Email == email);

        user.IsActive = false;
        await context.SaveChangesAsync();
    }
}
