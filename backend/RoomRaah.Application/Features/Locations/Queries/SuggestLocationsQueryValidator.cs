using FluentValidation;

namespace RoomRaah.Application.Features.Locations.Queries;

public class SuggestLocationsQueryValidator : AbstractValidator<SuggestLocationsQuery>
{
    public SuggestLocationsQueryValidator()
    {
        RuleFor(x => x.Q)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("q is required.")
            .Must(q => q!.Trim().Length >= SuggestLocationsQuery.MinQueryLength)
                .WithMessage($"q must be at least {SuggestLocationsQuery.MinQueryLength} characters.")
            .Must(q => q!.Trim().Length <= SuggestLocationsQuery.MaxQueryLength)
                .WithMessage($"q must be at most {SuggestLocationsQuery.MaxQueryLength} characters.")
            // The error is reported as "q", the name the caller actually sent.
            .OverridePropertyName("q");
    }
}
