using FluentValidation;

namespace RoomRaah.Application.Features.Messaging.Commands;

public class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("Property id must be greater than 0.");
    }
}
