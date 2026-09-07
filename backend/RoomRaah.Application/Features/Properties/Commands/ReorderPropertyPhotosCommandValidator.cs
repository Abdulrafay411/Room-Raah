using FluentValidation;

namespace RoomRaah.Application.Features.Properties.Commands;

public class ReorderPropertyPhotosCommandValidator : AbstractValidator<ReorderPropertyPhotosCommand>
{
    public ReorderPropertyPhotosCommandValidator()
    {
        RuleFor(x => x.PhotoIds)
            .NotEmpty().WithMessage("The new order must name at least one photo.");
    }
}
