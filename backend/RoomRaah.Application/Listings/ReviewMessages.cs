namespace RoomRaah.Application.Listings;

/// <summary>
/// Every message the owner-edit and admin-review paths can return, defined once so
/// the same rule reads the same way wherever it is enforced.
/// </summary>
public static class ReviewMessages
{
    public const string EditLockedWhileUnderReview =
        "This listing is being reviewed. It cannot be edited until the review is finished.";

    public const string NothingChanged =
        "This change is identical to the listing as it stands.";

    public const string RevisionAlreadyPending =
        "A change of this kind is already waiting for review on this listing.";

    public const string NotReviewable =
        "Only a listing that is waiting for review can be approved, rejected or returned for changes.";

    public const string RevisionNotPending =
        "This revision has already been decided.";

    public const string ChecksIncomplete =
        "A listing cannot be published until the owner identity, address, photo and facility checks have all passed.";

    public const string PhotoAlreadyPendingRemoval =
        "This photo is already waiting to be removed.";

    public const string NoteRequired =
        "A note is required so the owner knows what to change.";

    public static string RevisionNotFound(int id) => $"Revision {id} was not found.";
}
