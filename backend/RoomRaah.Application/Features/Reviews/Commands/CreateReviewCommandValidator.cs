using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reviews.Commands;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");

        // Rule 40.
        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("A comment is required.")
            .MaximumLength(1000).WithMessage("The comment may be at most 1000 characters.");

        RuleFor(x => x.StayStatus)
            .Must(BeAStayStatus)
            .WithMessage("Stay status must be Visited, CurrentlyStaying or PreviouslyStayed.");
    }

    private static bool BeAStayStatus(string? value) =>
        EnumName.IsDefined<StayStatus>(value);
}
