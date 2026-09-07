using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reviews.Commands;

public class ModerateReviewCommandValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewCommandValidator()
    {
        RuleFor(x => x.State)
            .Must(BeAModerationTarget)
            .WithMessage("State must be Approved or Hidden.");
    }

    /// <summary>
    /// Rule 96. Pending is where a review starts and not somewhere an admin puts one
    /// back: undoing an approval is hiding the review, which says what happened.
    /// </summary>
    private static bool BeAModerationTarget(string? value) =>
        EnumName.TryParse<ModerationState>(value, out var state) &&
        ModerationRules.IsModerationTarget(state);
}
