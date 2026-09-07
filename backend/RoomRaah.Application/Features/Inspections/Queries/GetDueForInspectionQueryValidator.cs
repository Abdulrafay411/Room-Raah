using FluentValidation;

namespace RoomRaah.Application.Features.Inspections.Queries;

public class GetDueForInspectionQueryValidator : AbstractValidator<GetDueForInspectionQuery>
{
    public GetDueForInspectionQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
