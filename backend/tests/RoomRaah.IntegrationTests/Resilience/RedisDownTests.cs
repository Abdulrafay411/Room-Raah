using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

namespace RoomRaah.IntegrationTests.Resilience;

/// <summary>
/// The same auth journey with no Redis at all. A cache that is down must slow the
/// product, never break it: rate limiting stops being enforced and the revocation
/// set stops being consulted, but nobody is locked out.
/// </summary>
public class RedisDownFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    // The seeder stores real photographs, so this host needs somewhere to put
    // them even though the subject of these tests is Redis.
    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync() =>
        await Task.WhenAll(_sqlServer.StartAsync(), _azurite.StartAsync());

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_sqlServer.DisposeAsync().AsTask(), _azurite.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _sqlServer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:AzureBlob", _azurite.GetConnectionString());

        // A port nothing is listening on. connectTimeout keeps each doomed attempt short.
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6399,connectTimeout=200,syncTimeout=200");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

public class RedisDownTests : IClassFixture<RedisDownFactory>
{
    private readonly RedisDownFactory _factory;
    private readonly HttpClient _client;

    public RedisDownTests(RedisDownFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"nocache-{Guid.NewGuid():N}@roomraah.local";

    [Fact]
    public async Task The_whole_sign_up_journey_still_works_without_redis()
    {
        var email = NewEmail();

        var register = await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        });

        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var code = _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);

        var verify = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code });

        verify.StatusCode.ShouldBe(HttpStatusCode.OK);

        var login = await _client.PostJsonAsync(
            "/api/v1/auth/login", new { Email = email, Password = "Password1" });

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_still_rotates_when_the_revocation_set_is_unreachable()
    {
        var email = NewEmail();

        await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        });

        var code = _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);
        var verify = await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code });

        var session = await verify.ReadAsync<AuthResponseDto>();

        var refresh = await _client.PostJsonAsync(
            "/api/v1/auth/refresh", new { RefreshToken = session.RefreshToken });

        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await refresh.ReadAsync<AuthResponseDto>()).RefreshToken.ShouldNotBe(session.RefreshToken);
    }

    [Fact]
    public async Task Logout_survives_a_failed_write_to_the_revocation_set()
    {
        var email = NewEmail();

        await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        });

        var code = _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail);
        var session = await (await _client.PostJsonAsync(
            "/api/v1/auth/verify-otp", new { Email = email, Code = code })).ReadAsync<AuthResponseDto>();

        var client = _factory.CreateClient();
        client.Authorise(session.AccessToken);

        var logout = await client.PostJsonAsync(
            "/api/v1/auth/logout", new { RefreshToken = session.RefreshToken });

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The database row is still stamped, so the token is dead even with no Redis.
        var afterLogout = await _client.PostJsonAsync(
            "/api/v1/auth/refresh", new { RefreshToken = session.RefreshToken });

        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rule_7_the_rate_limit_is_the_one_thing_that_stops_being_enforced()
    {
        var email = NewEmail();

        await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Integration Person",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Seeker"
        });

        for (var i = 0; i < 5; i++)
        {
            var resend = await _client.PostJsonAsync("/api/v1/auth/resend-otp", new { Email = email });
            resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }
}
