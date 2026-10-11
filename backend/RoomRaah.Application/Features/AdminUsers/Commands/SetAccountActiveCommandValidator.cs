using FluentValidation;

namespace RoomRaah.Application.Features.AdminUsers.Commands;

/// <summary>
/// Only the length. A missing reason is rule 119 and is answered by the handler, in the
/// order rule 119 asks for; a reason that is too long cannot be stored (rule 137).
/// </summary>
public class SetAccountActiveCommandValidator : AbstractValidator<SetAccountActiveCommand>
{
    public const int MaximumReasonLength = 1000;

    public SetAccountActiveCommandValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(MaximumReasonLength)
            .WithMessage($"The reason may not be longer than {MaximumReasonLength} characters.")
            .When(x => x.Reason is not null);
    }
}
