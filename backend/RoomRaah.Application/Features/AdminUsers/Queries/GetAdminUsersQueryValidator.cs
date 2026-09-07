using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminUsers.Queries;

public class GetAdminUsersQueryValidator : AbstractValidator<GetAdminUsersQuery>
{
    public GetAdminUsersQueryValidator()
    {
        // Through EnumName rather than Enum.TryParse, which accepts "5" for a
        // three-value enumeration and hands back a role nothing matches.
        RuleFor(x => x.Role)
            .Must(EnumName.IsDefined<UserRole>)
            .WithMessage("Role must be Seeker, Owner or Admin.")
            .When(x => !string.IsNullOrWhiteSpace(x.Role));

        // 200 rather than the 60 the autocomplete uses, because the thing an admin
        // most often pastes in here is a whole email address — and §Field Validation
        // allows an address up to 200 characters, so a shorter cap would refuse a
        // search for an account that legitimately exists.
        RuleFor(x => x.Q)
            .Length(2, 200).WithMessage("The search term must be between 2 and 200 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Q));

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");
    }
}
