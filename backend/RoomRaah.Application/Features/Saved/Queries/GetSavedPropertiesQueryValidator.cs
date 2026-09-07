using FluentValidation;

namespace RoomRaah.Application.Features.Saved.Queries;

public class GetSavedPropertiesQueryValidator : AbstractValidator<GetSavedPropertiesQuery>
{
    public GetSavedPropertiesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
