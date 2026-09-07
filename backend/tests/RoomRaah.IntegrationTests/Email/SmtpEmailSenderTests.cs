using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Email;
using Shouldly;

namespace RoomRaah.IntegrationTests.Email;

/// <summary>
/// The endpoint tests substitute SMTP so they can read the code. These tests exercise
/// the real sender against a real mail server, so the substitution never hides a
/// broken transport.
/// </summary>
public class SmtpEmailSenderTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/")))
        .Build();

    private SmtpEmailSender _sender = null!;
    private HttpClient _mailpitApi = null!;

    public async Task InitializeAsync()
    {
        await _mailpit.StartAsync();

        _sender = new SmtpEmailSender(
            Options.Create(new EmailSettings
            {
                Host = _mailpit.Hostname,
                Port = _mailpit.GetMappedPublicPort(1025),
                UseSsl = false,
                FromAddress = "noreply@roomraah.local",
                FromName = "RoomRaah",
                FrontendBaseUrl = "http://localhost:4200"
            }),
            NullLogger<SmtpEmailSender>.Instance);

        _mailpitApi = new HttpClient
        {
            BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}")
        };
    }

    public async Task DisposeAsync()
    {
        _mailpitApi.Dispose();
        await _mailpit.DisposeAsync();
    }

    private static AppUser Recipient(string email) => new()
    {
        Id = 1,
        FullName = "Integration Person",
        Email = email,
        PhoneNumber = "+923001112233"
    };

    private async Task<string> LatestBodyForAsync(string email)
    {
        var listing = await _mailpitApi.GetFromJsonAsync<JsonElement>("/api/v1/messages");

        var id = listing.GetProperty("messages")
            .EnumerateArray()
            .First(m => m.GetProperty("To")
                .EnumerateArray()
                .Any(t => t.GetProperty("Address").GetString() == email))
            .GetProperty("ID")
            .GetString();

        var message = await _mailpitApi.GetFromJsonAsync<JsonElement>($"/api/v1/message/{id}");
        return message.GetProperty("HTML").GetString()!;
    }

    [Fact]
    public async Task A_verification_code_reaches_the_inbox()
    {
        var email = $"smtp-otp-{Guid.NewGuid():N}@roomraah.local";

        await _sender.SendOtpAsync(Recipient(email), "482913", OtpPurpose.VerifyEmail);

        var body = await LatestBodyForAsync(email);

        body.ShouldContain("482913");
        body.ShouldContain("Integration Person");
    }

    [Fact]
    public async Task A_reset_email_carries_a_link_built_from_the_configured_front_end()
    {
        var email = $"smtp-reset-{Guid.NewGuid():N}@roomraah.local";

        await _sender.SendPasswordResetAsync(Recipient(email), "abc-123_token");

        var body = await LatestBodyForAsync(email);

        body.ShouldContain("http://localhost:4200/reset-password");
        body.ShouldContain("abc-123_token");
        body.ShouldContain(Uri.EscapeDataString(email));
    }
}
