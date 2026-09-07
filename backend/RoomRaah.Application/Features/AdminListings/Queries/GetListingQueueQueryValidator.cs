using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetListingQueueQueryValidator : AbstractValidator<GetListingQueueQuery>
{
    public GetListingQueueQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Status)
            .Must(BeAStatus).WithMessage(
                "Status must be Draft, PendingReview, ChangesRequested, Published, Rejected or Unpublished.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }

    private static bool BeAStatus(string? value) =>
        EnumName.IsDefined<PropertyStatus>(value);
}
