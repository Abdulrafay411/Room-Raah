using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendOtpAsync(AppUser user, string code, OtpPurpose purpose)
    {
        var body = $"""
            <p>Hello {user.FullName},</p>
            <p>Your RoomRaah verification code is:</p>
            <p style="font-size:28px;letter-spacing:6px;font-weight:700">{code}</p>
            <p>It expires in 10 minutes. If you did not ask for it, you can ignore this email.</p>
            <p>— RoomRaah</p>
            """;

        return SendAsync(user, "Your RoomRaah verification code", body, purpose.ToString());
    }

    public Task SendPasswordResetAsync(AppUser user, string token)
    {
        var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/reset-password" +
                   $"?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

        var body = $"""
            <p>Hello {user.FullName},</p>
            <p>Use the link below to choose a new RoomRaah password. It works once and expires in 30 minutes.</p>
            <p><a href="{link}">Reset your password</a></p>
            <p>If you did not ask for this, nothing has changed on your account.</p>
            <p>— RoomRaah</p>
            """;

        return SendAsync(user, "Reset your RoomRaah password", body, "PasswordReset");
    }

    public Task SendListingDecisionAsync(AppUser owner, string listingTitle, string decision, string? note)
    {
        var body = $"""
            <p>Hello {owner.FullName},</p>
            <p>Your listing <strong>{listingTitle}</strong> has been reviewed.</p>
            <p>Decision: <strong>{decision}</strong></p>
            {NoteBlock(note)}
            <p>You can see the full status on your RoomRaah dashboard.</p>
            <p>— RoomRaah</p>
            """;

        return SendAsync(owner, $"Your RoomRaah listing was reviewed: {decision}", body, "ListingDecision");
    }

    public Task SendRevisionDecisionAsync(
        AppUser owner, string listingTitle, RevisionKind kind, string decision, string? note)
    {
        var what = kind == RevisionKind.Availability ? "availability update" : "change";

        var body = $"""
            <p>Hello {owner.FullName},</p>
            <p>The {what} you submitted for <strong>{listingTitle}</strong> has been reviewed.</p>
            <p>Decision: <strong>{decision}</strong></p>
            {NoteBlock(note)}
            <p>— RoomRaah</p>
            """;

        return SendAsync(owner, $"Your RoomRaah {what} was reviewed: {decision}", body, "RevisionDecision");
    }

    public Task SendVisitRequestedAsync(
        AppUser owner, string listingTitle, VisitType visitType, DateTime preferredAt)
    {
        var kind = visitType == VisitType.Video ? "video visit" : "visit";

        var body = $"""
            <p>Hello {owner.FullName},</p>
            <p>Someone would like to arrange a {kind} to <strong>{listingTitle}</strong>.</p>
            <p>Preferred time: <strong>{preferredAt:dddd d MMMM yyyy, HH:mm} UTC</strong></p>
            <p>Sign in to RoomRaah to accept or decline it.</p>
            <p>&mdash; RoomRaah</p>
            """;

        return SendAsync(owner, $"A {kind} was requested for {listingTitle}", body, "VisitRequested");
    }

    public Task SendVisitResponseAsync(AppUser seeker, string listingTitle, string decision, string? note)
    {
        var body = $"""
            <p>Hello {seeker.FullName},</p>
            <p>Your visit request for <strong>{listingTitle}</strong> was <strong>{decision.ToLowerInvariant()}</strong>.</p>
            {NoteBlock(note)}
            <p>&mdash; RoomRaah</p>
            """;

        return SendAsync(seeker, $"Your RoomRaah visit request was {decision.ToLowerInvariant()}", body, "VisitResponse");
    }

    public Task SendMessageNudgeAsync(AppUser recipient, string listingTitle, string senderDisplayName)
    {
        // No message body, by design. The nudge tells them there is something to come
        // back for; the conversation itself stays on RoomRaah where it can be checked.
        var body = $"""
            <p>Hello {recipient.FullName},</p>
            <p><strong>{senderDisplayName}</strong> sent you a message about <strong>{listingTitle}</strong>.</p>
            <p>Sign in to RoomRaah to read it and reply.</p>
            <p>&mdash; RoomRaah</p>
            """;

        return SendAsync(recipient, "You have a new RoomRaah message", body, "MessageNudge");
    }

    public Task SendStaleListingNudgeAsync(AppUser owner, string listingTitle, DateTime lastConfirmedAt)
    {
        var body = $"""
            <p>Hello {owner.FullName},</p>
            <p>Your listing <strong>{listingTitle}</strong> has not had its availability
            confirmed since <strong>{lastConfirmedAt:d MMMM yyyy}</strong>.</p>
            <p>Seekers rely on that date, so a listing that has gone quiet is one they
            stop trusting. Sign in and confirm the free beds, or update the count.</p>
            <p>&mdash; RoomRaah</p>
            """;

        return SendAsync(owner, "Confirm your RoomRaah listing's availability", body, "StaleListing");
    }

    private static string NoteBlock(string? note) =>
        string.IsNullOrWhiteSpace(note)
            ? string.Empty
            : $"<p>What the reviewer wrote:</p><blockquote>{note}</blockquote>";

    private async Task SendAsync(AppUser user, string subject, string htmlBody, string template)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(user.FullName, user.Email));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = _settings.UseSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions);

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            await client.AuthenticateAsync(_settings.UserName, _settings.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("Email {Template} sent to user {UserId}", template, user.Id);
    }
}
