using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CreateVisitRequestCommandValidator : AbstractValidator<CreateVisitRequestCommand>
{
    /// <summary>
    /// The far edge of a visit request. Not in the original field table, and added
    /// because "in the future" alone accepts a request for the year 3000 — which
    /// would sit at the top of an owner's list forever and can never be completed.
    /// </summary>
    public const int MaximumDaysAhead = 60;

    public CreateVisitRequestCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");

        RuleFor(x => x.VisitType)
            .Must(BeAVisitType).WithMessage("Visit type must be Physical or Video.");

        RuleFor(x => x.PreferredAt)
            .Must(at => at > DateTime.UtcNow)
            .WithMessage("The preferred time must be in the future.")
            .Must(at => at <= DateTime.UtcNow.AddDays(MaximumDaysAhead))
            .WithMessage($"The preferred time must be within {MaximumDaysAhead} days.");
    }

    private static bool BeAVisitType(string? value) =>
        EnumName.IsDefined<VisitType>(value);
}
