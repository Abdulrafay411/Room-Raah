using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reports.Queries;

public class GetReportQueueQueryValidator : AbstractValidator<GetReportQueueQuery>
{
    public GetReportQueueQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(BeAStatus).WithMessage("Status must be Open, UnderReview, Upheld or Dismissed.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }

    private static bool BeAStatus(string? value) =>
        EnumName.IsDefined<ReportStatus>(value);
}
