using FluentValidation;

namespace RoomRaah.Application.Features.Properties.Commands;

public class ConfirmAvailabilityCommandValidator : AbstractValidator<ConfirmAvailabilityCommand>
{
    public ConfirmAvailabilityCommandValidator()
    {
        RuleFor(x => x.AvailableBeds)
            .NotNull().WithMessage("Available beds is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Available beds cannot be negative.");
    }
}
