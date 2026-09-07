using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Visits.Commands;

public class RespondToVisitCommandValidator : AbstractValidator<RespondToVisitCommand>
{
    public RespondToVisitCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(BeAnAnswer).WithMessage("Status must be Accepted or Declined.");

        RuleFor(x => x.OwnerResponseNote)
            .MaximumLength(1000).WithMessage("The note may be at most 1000 characters.");
    }

    /// <summary>
    /// Rule 88 names two answers, and the other three states are not answers: a
    /// visit is completed and cancelled through their own endpoints, by people this
    /// one does not necessarily let in.
    /// </summary>
    private static bool BeAnAnswer(string? value) =>
        EnumName.TryParse<VisitStatus>(value, out var status) &&
        status is VisitStatus.Accepted or VisitStatus.Declined;
}
