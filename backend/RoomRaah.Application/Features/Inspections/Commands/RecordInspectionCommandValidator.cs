using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Inspections.Commands;

public class RecordInspectionCommandValidator : AbstractValidator<RecordInspectionCommand>
{
    public const int MaximumNotesLength = 2000;

    public RecordInspectionCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");

        RuleFor(x => x.Result)
            .Must(r => EnumName.IsDefined<InspectionResult>(r))
            .WithMessage("Result must be Passed or Failed.");

        // An inspection is a record of a visit that happened. A future date here would
        // be a scheduled visit, which rule 115 says does not exist anywhere in this
        // product — including in a row somebody entered by hand.
        RuleFor(x => x.InspectedAt)
            .NotNull().WithMessage("The inspection date is required.")
            .Must(at => at <= DateTime.UtcNow)
            .WithMessage("An inspection cannot be recorded for a date in the future.");

        RuleFor(x => x.Notes)
            .MaximumLength(MaximumNotesLength)
            .WithMessage($"Notes may be at most {MaximumNotesLength} characters.");

        RuleFor(x => x.FeeAmount)
            .GreaterThanOrEqualTo(0).WithMessage("A fee cannot be negative.")
            .When(x => x.FeeAmount.HasValue);

        RuleFor(x => x.FeeCollectedAt)
            .Must(at => at <= DateTime.UtcNow)
            .WithMessage("A fee cannot be collected in the future.")
            .When(x => x.FeeCollectedAt.HasValue);
    }
}
