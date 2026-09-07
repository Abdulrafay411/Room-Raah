using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Auth.Commands;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(120).WithMessage("Full name must not exceed 120 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .Matches(@"^\+?[0-9]+$").WithMessage("Phone number may contain digits and a leading + only.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(BeSelfServiceRole)
            .WithMessage("Role must be either Seeker or Owner.");

        RuleFor(x => x.CnicNumber)
            .NotEmpty().WithMessage("CNIC number is required for an owner account.")
            .Matches("^[0-9]{13}$").WithMessage("CNIC number must be exactly 13 digits.")
            .When(x => IsOwner(x.Role));

        RuleFor(x => x.PayoutContact)
            .MaximumLength(200).WithMessage("Payout contact must not exceed 200 characters.");
    }

    private static bool BeSelfServiceRole(string role)
    {
        return EnumName.TryParse<UserRole>(role, out var parsed)
               && parsed != UserRole.Admin;
    }

    private static bool IsOwner(string role)
    {
        return EnumName.TryParse<UserRole>(role, out var parsed)
               && parsed == UserRole.Owner;
    }
}
