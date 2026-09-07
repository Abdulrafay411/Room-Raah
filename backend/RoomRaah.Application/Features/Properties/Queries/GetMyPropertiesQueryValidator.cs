using FluentValidation;

namespace RoomRaah.Application.Features.Properties.Queries;

public class GetMyPropertiesQueryValidator : AbstractValidator<GetMyPropertiesQuery>
{
    public GetMyPropertiesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.")
            .OverridePropertyName("page");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetMyPropertiesQuery.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {GetMyPropertiesQuery.MaxPageSize}.")
            .OverridePropertyName("pageSize");
    }
}
