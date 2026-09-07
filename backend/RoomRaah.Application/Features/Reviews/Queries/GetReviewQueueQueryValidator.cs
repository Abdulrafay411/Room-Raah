using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reviews.Queries;

public class GetReviewQueueQueryValidator : AbstractValidator<GetReviewQueueQuery>
{
    public GetReviewQueueQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.State)
            .Must(BeAState).WithMessage("State must be Pending, Approved or Hidden.")
            .When(x => !string.IsNullOrWhiteSpace(x.State));
    }

    private static bool BeAState(string? value) =>
        EnumName.IsDefined<ModerationState>(value);
}
