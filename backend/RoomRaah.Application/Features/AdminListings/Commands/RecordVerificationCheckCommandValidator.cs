using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RecordVerificationCheckCommandValidator : AbstractValidator<RecordVerificationCheckCommand>
{
    public RecordVerificationCheckCommandValidator()
    {
        RuleFor(x => x.CheckType)
            .NotEmpty().WithMessage("Check type is required.")
            .Must(BeACheckType).WithMessage(
                "Check type must be OwnerIdentity, Address, Photos, Facilities or Availability.");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(BeAStatus).WithMessage("Status must be NotChecked, Checked or Failed.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }

    private static bool BeACheckType(string value) =>
        EnumName.IsDefined<VerificationCheckType>(value);

    private static bool BeAStatus(string value) =>
        EnumName.IsDefined<VerificationStatus>(value);
}
