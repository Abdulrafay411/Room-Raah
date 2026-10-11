using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Inspections;

/// <summary>
/// The badge's rules, in one place so they can be walked over rather than trusted.
///
/// The same reason <c>VisitLifecycle</c> and <c>ModerationRules</c> exist: a rule
/// asked inside a handler is a rule tested through whatever states that handler's
/// tests happened to set up, and a rule asked here is a rule a theory can walk.
/// </summary>
public static class BadgeRules
{
    /// <summary>
    /// How long a badged listing may go between visits before the admin's worklist
    /// picks it up. Rule 114.
    ///
    /// §Business Model says the team re-inspects roughly monthly and that the monthly
    /// fee means monthly contact, so thirty days is the promise rather than a guess.
    /// </summary>
    public static readonly TimeSpan ReinspectionInterval = TimeSpan.FromDays(30);

    /// <summary>Rule 109 and rule 110: recording and granting are the inspector's, removing is any admin's.</summary>
    public static bool CanInspect(AppUser admin) =>
        admin.Role == UserRole.Admin && admin.AdminProfile?.CanInspect == true;

    public static void RequireInspector(AppUser admin)
    {
        if (!CanInspect(admin))
        {
            throw new UnauthorizedAccessException(InspectionMessages.NotAnInspector);
        }
    }

    /// <summary>
    /// Rule 110. The *most recent* inspection, not any passed one: a listing that
    /// passed in March and failed in April has a passed inspection in its history and
    /// has no business carrying a badge.
    /// </summary>
    public static bool MayGrant(Property property, InspectionResult? latest) =>
        !property.HasInspectionBadge && latest == InspectionResult.Passed;

    public static void RequireGrantable(Property property, InspectionResult? latest)
    {
        if (property.HasInspectionBadge)
        {
            throw new BusinessRuleException(InspectionMessages.BadgeAlreadyGranted);
        }

        if (latest != InspectionResult.Passed)
        {
            throw new BusinessRuleException(InspectionMessages.NoPassedInspection);
        }
    }

    /// <summary>Rule 111.</summary>
    public static void RequireRemovable(Property property)
    {
        if (!property.HasInspectionBadge)
        {
            throw new BusinessRuleException(InspectionMessages.BadgeNotGranted);
        }
    }

    /// <summary>
    /// Rule 112. A reason is required on both actions, and it is a 400 rather than a
    /// 422 for the same reason rule 26's note is: an absent field is a rule about the
    /// shape of the request, not about the state of the listing.
    /// </summary>
    /// <summary>
    /// Rule 138. The removal an owner should be told about: the badge's most recent
    /// event, when that is a removal and the listing does not carry the badge. Null
    /// otherwise - a badge granted again has nothing left to explain.
    /// </summary>
    public static BadgeEvent? CurrentRemoval(Property property)
    {
        if (property.HasInspectionBadge)
        {
            return null;
        }

        var latest = property.BadgeEvents
            .OrderByDescending(e => e.ActedAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();

        return latest?.Action == BadgeAction.Removed ? latest : null;
    }

    public static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "Reason", InspectionMessages.ReasonRequired)
            ]);
        }

        return reason.Trim();
    }

    /// <summary>Rule 114: badged, published, and not visited inside the interval.</summary>
    public static DateTime DueCutoff(DateTime asOf) => asOf - ReinspectionInterval;
}
