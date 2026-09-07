using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Seekers;

/// <summary>
/// Rules 88, 90 and 91 in one place, as predicates over a status rather than as
/// conditions buried in three handlers. The predicates exist so that a test can walk
/// every value of <see cref="VisitStatus"/> and assert what each one permits: a
/// transition rule tested only through the states a test happens to set up is a rule
/// that holds for those states and no others.
/// </summary>
public static class VisitLifecycle
{
    /// <summary>Rule 88: an answer is given to a request that is still waiting for one.</summary>
    public static bool CanAnswer(VisitStatus status) => status == VisitStatus.Requested;

    /// <summary>Rule 90: only a visit the owner agreed to can have happened.</summary>
    public static bool CanComplete(VisitStatus status) => status == VisitStatus.Accepted;

    /// <summary>Rule 91: a seeker withdraws a request that has not yet been closed.</summary>
    public static bool CanCancel(VisitStatus status) =>
        status is VisitStatus.Requested or VisitStatus.Accepted;

    /// <summary>Rule 88.</summary>
    public static void RequireAnswerable(VisitRequest request)
    {
        if (!CanAnswer(request.Status))
        {
            throw new BusinessRuleException(SeekerMessages.VisitNotAnswerable);
        }
    }

    /// <summary>
    /// Rule 90, both halves. The second is the one that matters: a visit whose time
    /// has not arrived has not happened, and marking it complete would open rule 38's
    /// gate to a review of a room nobody has seen.
    /// </summary>
    public static void RequireCompletable(VisitRequest request, DateTime now)
    {
        if (!CanComplete(request.Status))
        {
            throw new BusinessRuleException(SeekerMessages.VisitNotCompletable);
        }

        if (request.PreferredAt > now)
        {
            throw new BusinessRuleException(SeekerMessages.VisitNotYetHappened);
        }
    }

    /// <summary>
    /// Rule 89. A seeker told only "declined" has been given nothing to act on and
    /// asks again; a seeker told the room is taken until March has been answered.
    /// A validation failure rather than a business rule, because the spec answers
    /// this with 400 and because it names the field to fix.
    /// </summary>
    public static string? RequireNoteOnDecline(VisitStatus answer, string? note)
    {
        var trimmed = note?.Trim();

        if (answer == VisitStatus.Declined && string.IsNullOrWhiteSpace(trimmed))
        {
            throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    "ownerResponseNote", SeekerMessages.DeclineNeedsNote)
            ]);
        }

        return trimmed;
    }

    /// <summary>Rule 91.</summary>
    public static void RequireCancellable(VisitRequest request)
    {
        if (!CanCancel(request.Status))
        {
            throw new BusinessRuleException(SeekerMessages.VisitNotCancellable);
        }
    }
}
