using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for SMTP during the endpoint tests so a test can read the code that was
/// sent. The real <c>SmtpEmailSender</c> is exercised separately against Mailpit.
/// </summary>
public class CapturingEmailSender : IEmailSender
{
    public record SentEmail(string Email, string Secret, OtpPurpose Purpose);

    private readonly List<SentEmail> _sent = [];
    private readonly Lock _gate = new();

    private SentEmail[] Snapshot()
    {
        lock (_gate)
        {
            return _sent.ToArray();
        }
    }

    private void Record(SentEmail email)
    {
        lock (_gate)
        {
            _sent.Add(email);
        }
    }

    public Task SendOtpAsync(AppUser user, string code, OtpPurpose purpose)
    {
        Record(new SentEmail(user.Email, code, purpose));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(AppUser user, string token)
    {
        Record(new SentEmail(user.Email, token, OtpPurpose.ResetPassword));
        return Task.CompletedTask;
    }

    /// <summary>One decision email: who it went to, what it said, and the note.</summary>
    public record SentDecision(string Email, string Subject, string Decision, string? Note);

    private readonly List<SentDecision> _decisions = [];

    public Task SendListingDecisionAsync(AppUser owner, string listingTitle, string decision, string? note)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(owner.Email, listingTitle, decision, note));
        }

        return Task.CompletedTask;
    }

    public Task SendRevisionDecisionAsync(
        AppUser owner, string listingTitle, RevisionKind kind, string decision, string? note)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(owner.Email, $"{listingTitle} ({kind})", decision, note));
        }

        return Task.CompletedTask;
    }

    public Task SendVisitRequestedAsync(
        AppUser owner, string listingTitle, VisitType visitType, DateTime preferredAt)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(
                owner.Email, listingTitle, $"VisitRequested:{visitType}", preferredAt.ToString("O")));
        }

        return Task.CompletedTask;
    }

    public Task SendVisitResponseAsync(AppUser seeker, string listingTitle, string decision, string? note)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(seeker.Email, listingTitle, decision, note));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rule 105's nudge. Recorded as a decision so a test can count how many went out —
    /// which is the whole point of the throttle — and note that the payload carries the
    /// listing and the sender's name and no message body, because the email does not.
    /// </summary>
    public Task SendMessageNudgeAsync(AppUser recipient, string listingTitle, string senderDisplayName)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(
                recipient.Email, listingTitle, "MessageNudge", senderDisplayName));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rule 125's nudge, recorded so a test can count how many an owner received —
    /// which is what the seven-day throttle is for.
    /// </summary>
    public Task SendStaleListingNudgeAsync(AppUser owner, string listingTitle, DateTime lastConfirmedAt)
    {
        lock (_gate)
        {
            _decisions.Add(new SentDecision(
                owner.Email, listingTitle, "StaleListing", lastConfirmedAt.ToString("O")));
        }

        return Task.CompletedTask;
    }

    public SentDecision[] DecisionsFor(string email)
    {
        lock (_gate)
        {
            return _decisions
                .Where(d => d.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public string LatestSecretFor(string email, OtpPurpose purpose)
    {
        var sent = Snapshot();

        var match = sent
            .Where(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && e.Purpose == purpose)
            .LastOrDefault();

        if (match is null)
        {
            throw new InvalidOperationException(
                $"No {purpose} email was sent to {email}. Sent: {sent.Length}.");
        }

        return match.Secret;
    }

    public int CountFor(string email)
    {
        return Snapshot().Count(e => e.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }
}
