using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reports.Commands;

public class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");

        RuleFor(x => x.Reason)
            .Must(BeAReason).WithMessage(
                "Reason must be Inaccurate, Misleading, AlreadyTaken, Scam, Offensive or Other.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("Details are required.")
            .MaximumLength(1000).WithMessage("Details may be at most 1000 characters.");
    }

    private static bool BeAReason(string? value) =>
        EnumName.IsDefined<ReportReason>(value);
}
