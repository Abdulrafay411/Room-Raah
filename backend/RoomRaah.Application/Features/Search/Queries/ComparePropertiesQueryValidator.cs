using FluentValidation;
using RoomRaah.Application.Listings;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>
/// Only the shape of <c>ids</c> is decided here. How many of them there are is rule
/// 35, which answers 422 and therefore belongs to the handler: a well-formed request
/// asking for four listings is refused by the product, not by its parser.
/// </summary>
public class ComparePropertiesQueryValidator : AbstractValidator<ComparePropertiesQuery>
{
    public ComparePropertiesQueryValidator()
    {
        RuleFor(x => x.Ids)
            .NotEmpty().WithMessage("At least one listing id is required.")
            .Must(v => IdList.TryParse(v, out var ids) && ids.Count > 0)
            .WithMessage("Ids must be a comma-separated list of numbers.")
            .OverridePropertyName("ids");
    }
}
