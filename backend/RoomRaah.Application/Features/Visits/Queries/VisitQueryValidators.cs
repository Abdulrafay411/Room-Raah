using FluentValidation;

namespace RoomRaah.Application.Features.Visits.Queries;

public class GetMyVisitsQueryValidator : AbstractValidator<GetMyVisitsQuery>
{
    public GetMyVisitsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}

public class GetOwnerVisitsQueryValidator : AbstractValidator<GetOwnerVisitsQuery>
{
    public GetOwnerVisitsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
