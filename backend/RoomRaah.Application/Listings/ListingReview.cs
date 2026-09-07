using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Listings;

/// <summary>
/// The checks every admin decision has to make, in one place for the same reason
/// <see cref="OwnedListing"/> exists: a handler cannot enforce one of them and quietly
/// forget the other.
/// </summary>
public static class ListingReview
{
    /// <summary>
    /// Rule 23. Availability is a check an admin can record, but it is not one of the
    /// four that gate publishing: a bed count is confirmed continuously afterwards,
    /// and holding a listing back for it would stop the site ever going live.
    /// </summary>
    public static readonly VerificationCheckType[] RequiredToPublish =
    [
        VerificationCheckType.OwnerIdentity,
        VerificationCheckType.Address,
        VerificationCheckType.Photos,
        VerificationCheckType.Facilities
    ];

    public static bool ChecksPass(Property property) =>
        RequiredToPublish.All(type => property.VerificationChecks
            .Any(c => c.CheckType == type && c.Status == VerificationStatus.Checked));

    /// <summary>
    /// Rule 62, and from Phase 7 rules 122 and 124 as well.
    ///
    /// The statuses a decision may start from are the decision's own business rather
    /// than a constant here: approving accepts a listing waiting for review <em>and</em>
    /// one an admin took off the site, unpublishing accepts only a published one, and
    /// rejecting accepts neither of those. The message travels with the list because a
    /// refusal that says "only a listing waiting for review" to somebody trying to
    /// unpublish a draft is a refusal that explains the wrong rule.
    /// </summary>
    public static void RequireStatus(
        Property property, PropertyStatus[] allowed, string message)
    {
        if (!allowed.Contains(property.Status))
        {
            throw new BusinessRuleException(message);
        }
    }

    /// <summary>
    /// Rules 26 and 63. A refusal with no note is a dead end: the owner is told no
    /// and given nothing to act on, and writes back to ask what was wrong.
    /// </summary>
    public static string RequireNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("AdminNote", ReviewMessages.NoteRequired)
            ]);
        }

        return note.Trim();
    }
}
