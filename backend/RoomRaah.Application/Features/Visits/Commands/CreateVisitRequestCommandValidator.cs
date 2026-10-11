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

    /// <summary>
    /// How far back a preferred time may be. "Today" is the rule, so a visit arranged
    /// for this morning can still be recorded; the server does not know the person's
    /// time zone, so it allows the last day rather than its own UTC midnight. The client
    /// holds the person to their own local midnight, which always falls inside this.
    /// </summary>
    public const int MaximumHoursBehind = 24;

    public CreateVisitRequestCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");

        RuleFor(x => x.VisitType)
            .Must(BeAVisitType).WithMessage("Visit type must be Physical or Video.");

        RuleFor(x => x.PreferredAt)
            .Must(at => at >= DateTime.UtcNow.AddHours(-MaximumHoursBehind))
            .WithMessage("The preferred time may not be before today.")
            .Must(at => at <= DateTime.UtcNow.AddDays(MaximumDaysAhead))
            .WithMessage($"The preferred time must be within {MaximumDaysAhead} days.");
    }

    private static bool BeAVisitType(string? value) =>
        EnumName.IsDefined<VisitType>(value);
}
