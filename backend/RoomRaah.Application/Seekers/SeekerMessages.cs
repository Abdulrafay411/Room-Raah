namespace RoomRaah.Application.Seekers;

/// <summary>
/// What this area says when it refuses. Nothing here names the other party: a
/// seeker is told a visit request is not theirs, not whose it is, and an owner is
/// told the same. Rule 33 has no exception for an error message.
/// </summary>
public static class SeekerMessages
{
    public const string AlreadySaved = "This listing is already on your shortlist.";
    public const string NotSaved = "This listing is not on your shortlist.";

    public const string VisitAlreadyOpen =
        "You already have an open visit request for this listing.";

    public const string VisitNotYours = "This visit request is not yours.";

    public static string VisitNotFound(int id) => $"No visit request with id {id}.";

    public const string VisitNotAnswerable =
        "Only a visit request that is still waiting for an answer can be accepted or declined.";

    public const string VisitNotCompletable =
        "Only an accepted visit request can be completed.";

    public const string VisitNotYetHappened =
        "A visit cannot be completed before the time it was arranged for.";

    public const string VisitNotCancellable =
        "Only a visit request that is still requested or accepted can be cancelled.";

    public const string DeclineNeedsNote = "A note is required when a visit request is declined.";

    public const string ReviewNeedsCompletedVisit =
        "You can review a listing only after a visit request on it has been completed.";

    public const string AlreadyReviewed = "You have already reviewed this listing.";

    public static string ReviewNotFound(int id) => $"No review with id {id}.";

    public const string ModerationStateInvalid =
        "A review is moderated to Approved or Hidden.";

    public const string ReportAlreadyOpen =
        "You already have a report on this listing waiting to be looked at.";

    public static string ReportNotFound(int id) => $"No report with id {id}.";

    public const string ReportAlreadyResolved = "This report has already been resolved.";

    public const string ReportStatusInvalid =
        "A report is moved to UnderReview, Upheld or Dismissed.";

    public const string ResolutionNeedsNote =
        "A note is required when a report is upheld or dismissed.";
}
