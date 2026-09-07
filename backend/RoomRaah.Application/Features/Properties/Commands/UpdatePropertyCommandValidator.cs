using FluentValidation;
using RoomRaah.Application.Listings;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UpdatePropertyCommandValidator : ListingFieldRules<UpdatePropertyCommand>
{
    public UpdatePropertyCommandValidator()
    {
        RuleFor(x => x.PropertyId)
            .GreaterThan(0).WithMessage("A listing id is required.");
    }
}
