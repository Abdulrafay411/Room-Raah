using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IEmailSender
{
    Task SendOtpAsync(AppUser user, string code, OtpPurpose purpose);
    Task SendPasswordResetAsync(AppUser user, string token);

    /// <summary>
    /// Rule 70. The decision and the note travel together: a refusal without the
    /// reason is a dead end, and the owner writes back to ask what was wrong.
    /// </summary>
    Task SendListingDecisionAsync(AppUser owner, string listingTitle, string decision, string? note);

    Task SendRevisionDecisionAsync(
        AppUser owner, string listingTitle, RevisionKind kind, string decision, string? note);

    /// <summary>
    /// Rule 92. Not in the original notification table, and added because it is the
    /// only thing that tells an owner a request is waiting: there is no phone number
    /// and no WhatsApp link anywhere in this product.
    /// </summary>
    Task SendVisitRequestedAsync(
        AppUser owner, string listingTitle, VisitType visitType, DateTime preferredAt);

    /// <summary>
    /// Rule 92, the other direction. Rule 89 is why the note travels with it: a
    /// seeker told only "declined" asks again.
    /// </summary>
    Task SendVisitResponseAsync(AppUser seeker, string listingTitle, string decision, string? note);

    /// <summary>
    /// Rule 105. Sent only when the recipient holds no live hub connection, and at
    /// most once an hour per conversation per recipient.
    ///
    /// It says a message is waiting and never what it said. Quoting the message would
    /// copy a private conversation into an inbox this product does not control, which
    /// is the opposite of the reason the conversation is kept on the site at all.
    /// </summary>
    Task SendMessageNudgeAsync(AppUser recipient, string listingTitle, string senderDisplayName);

    /// <summary>
    /// Rule 125. Freshness is one of the five things this product promises, and a
    /// listing nobody has confirmed in a month is the promise quietly going stale. The
    /// email asks the owner to re-confirm or update, and says when it was last done.
    /// </summary>
    Task SendStaleListingNudgeAsync(AppUser owner, string listingTitle, DateTime lastConfirmedAt);
}
