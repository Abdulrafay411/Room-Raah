using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Seekers;

/// <summary>
/// What an admin may do to a review and to a report. Both are written as predicates
/// over the target state for the same reason the visit transitions are: an
/// enumeration gains values, and a rule expressed as a list of what is allowed keeps
/// refusing the ones nobody has thought about yet.
/// </summary>
public static class ModerationRules
{
    /// <summary>Rule 96: an admin moves a review to Approved or Hidden, never back to Pending.</summary>
    public static bool IsModerationTarget(ModerationState state) =>
        state is ModerationState.Approved or ModerationState.Hidden;

    /// <summary>Rule 97: the three states an admin may set on a report.</summary>
    public static bool IsResolutionTarget(ReportStatus status) =>
        status is ReportStatus.UnderReview or ReportStatus.Upheld or ReportStatus.Dismissed;

    /// <summary>Rule 97: upholding and dismissing are verdicts, and a verdict says why.</summary>
    public static bool IsVerdict(ReportStatus status) =>
        status is ReportStatus.Upheld or ReportStatus.Dismissed;

    /// <summary>Rule 97: a report that already carries a verdict is not re-decided.</summary>
    public static bool IsResolved(ReportStatus status) => IsVerdict(status);

    public static void RequireModerationTarget(ModerationState state)
    {
        if (!IsModerationTarget(state))
        {
            throw new BusinessRuleException(SeekerMessages.ModerationStateInvalid);
        }
    }

    public static void RequireResolvable(Report report, ReportStatus target)
    {
        if (!IsResolutionTarget(target))
        {
            throw new BusinessRuleException(SeekerMessages.ReportStatusInvalid);
        }

        if (IsResolved(report.Status))
        {
            throw new BusinessRuleException(SeekerMessages.ReportAlreadyResolved);
        }
    }

    /// <summary>
    /// Rule 97, and the same shape as rules 26 and 63: a verdict says why. Thrown as
    /// a validation failure rather than a business rule, because the spec answers a
    /// missing note with 400 and because the field the reader has to fix is named.
    /// </summary>
    public static string? RequireNoteOnVerdict(ReportStatus target, string? note)
    {
        var trimmed = note?.Trim();

        if (IsVerdict(target) && string.IsNullOrWhiteSpace(trimmed))
        {
            throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "adminNote", SeekerMessages.ResolutionNeedsNote)
            ]);
        }

        return trimmed;
    }
}
