using FluentValidation;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public const int MaximumBodyLength = 2000;

    public SendMessageCommandValidator()
    {
        // Rule 45. NotEmpty rather than a length floor of one, because a body of
        // nothing but spaces is an empty message with a length of four.
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("A message cannot be empty.")
            .MaximumLength(MaximumBodyLength)
            .WithMessage($"A message may be at most {MaximumBodyLength} characters.");
    }
}
