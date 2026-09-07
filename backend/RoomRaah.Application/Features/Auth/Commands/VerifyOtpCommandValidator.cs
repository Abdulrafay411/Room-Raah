using FluentValidation;

namespace RoomRaah.Application.Features.Auth.Commands;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.")
            .Matches("^[0-9]{6}$").WithMessage("Verification code must be 6 digits.");
    }
}
