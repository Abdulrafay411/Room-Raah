using FluentValidation;

namespace RoomRaah.Application.Features.Locations.Queries;

public class GetAreasQueryValidator : AbstractValidator<GetAreasQuery>
{
    public GetAreasQueryValidator()
    {
        RuleFor(x => x.CityId)
            .GreaterThan(0).WithMessage("CityId must be greater than 0.");
    }
}
