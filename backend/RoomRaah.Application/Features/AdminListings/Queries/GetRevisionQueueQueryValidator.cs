using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetRevisionQueueQueryValidator : AbstractValidator<GetRevisionQueueQuery>
{
    public GetRevisionQueueQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.Kind)
            .Must(BeAKind).WithMessage("Kind must be either Content or Availability.")
            .When(x => !string.IsNullOrWhiteSpace(x.Kind));
    }

    private static bool BeAKind(string? value) => EnumName.IsDefined<RevisionKind>(value);
}
