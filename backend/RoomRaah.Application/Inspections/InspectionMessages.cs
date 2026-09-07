namespace RoomRaah.Application.Inspections;

/// <summary>
/// Every refusal the inspection, badge, unpublish and admin user paths can give.
/// One file, so the same rule reads the same way wherever it is enforced — and so
/// that nothing here can quietly start naming a person.
/// </summary>
public static class InspectionMessages
{
    // ------------------------------------------------------------- inspections

    public const string InspectionAlreadyRequested =
        "An inspection has already been requested for this listing. "
        + "After the first visit, RoomRaah re-inspects on its own schedule.";

    public const string OnlyPublishedCanBeInspected =
        "Only a published listing can be inspected.";

    public const string NotAnInspector =
        "This account is not permitted to record inspections.";

    // ------------------------------------------------------------------ badge

    public const string BadgeAlreadyGranted =
        "This listing already carries the inspection badge.";

    public const string BadgeNotGranted =
        "This listing does not carry the inspection badge.";

    public const string NoPassedInspection =
        "The badge can only be granted after a passed inspection.";

    public const string ReasonRequired =
        "A reason is required, because every badge change is recorded with one.";

    // -------------------------------------------------------------- unpublish

    public const string NotPublished =
        "Only a published listing can be taken off the site.";

    // ------------------------------------------------------------ admin users

    public const string CannotDeactivateSelf =
        "An account cannot deactivate itself.";

    public const string CannotChangeAnotherAdmin =
        "An admin account cannot be deactivated or reactivated here.";

    public const string AlreadyDeactivated =
        "This account is already deactivated.";

    public const string AlreadyActive =
        "This account is already active.";

    public static string UserNotFound(int id) => $"Account {id} was not found.";

    public static string InspectionNotFound(int id) => $"Inspection {id} was not found.";
}
