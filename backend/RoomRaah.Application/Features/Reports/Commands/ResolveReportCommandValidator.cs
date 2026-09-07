using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reports.Commands;

public class ResolveReportCommandValidator : AbstractValidator<ResolveReportCommand>
{
    public ResolveReportCommandValidator()
    {
        RuleFor(x => x.Status)
            .Must(BeAResolution)
            .WithMessage("Status must be UnderReview, Upheld or Dismissed.");

        RuleFor(x => x.AdminNote)
            .MaximumLength(1000).WithMessage("The note may be at most 1000 characters.");
    }

    /// <summary>
    /// Rule 97. Open is where a report starts and not somewhere an admin puts one
    /// back — a report that has been read and set aside is dismissed, which says so.
    /// </summary>
    private static bool BeAResolution(string? value) =>
        EnumName.TryParse<ReportStatus>(value, out var status) &&
        ModerationRules.IsResolutionTarget(status);
}
