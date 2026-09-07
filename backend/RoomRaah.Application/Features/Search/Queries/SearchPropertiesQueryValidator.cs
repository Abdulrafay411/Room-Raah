using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>
/// Everything here is decidable from the query string alone. Whether an id names a
/// row is not, so rule 85 lives in the handler and answers 422; these answer 400.
/// </summary>
public class SearchPropertiesQueryValidator : AbstractValidator<SearchPropertiesQuery>
{
    public SearchPropertiesQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.")
            .OverridePropertyName("page");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, SearchPropertiesQuery.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {SearchPropertiesQuery.MaxPageSize}.")
            .OverridePropertyName("pageSize");

        RuleFor(x => x.RoomType)
            .Must(v => EnumName.IsDefined<RoomType>(v))
            .WithMessage("Room type must be Private or Shared.")
            .When(x => !string.IsNullOrWhiteSpace(x.RoomType))
            .OverridePropertyName("roomType");

        RuleFor(x => x.GenderPolicy)
            .Must(v => EnumName.IsDefined<GenderPolicy>(v))
            .WithMessage("Gender policy must be Boys, Girls or Family.")
            .When(x => !string.IsNullOrWhiteSpace(x.GenderPolicy))
            .OverridePropertyName("genderPolicy");

        RuleFor(x => x.Sort)
            .Must(v => EnumName.IsDefined<PropertySort>(v))
            .WithMessage("Sort must be Recent, PriceLowToHigh, PriceHighToLow or Distance.")
            .When(x => !string.IsNullOrWhiteSpace(x.Sort))
            .OverridePropertyName("sort");

        RuleFor(x => x.FacilityIds)
            .Must(v => IdList.TryParse(v, out _))
            .WithMessage("Facility ids must be a comma-separated list of numbers.")
            .OverridePropertyName("facilityIds");

        RuleFor(x => x.MinRent)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum rent may not be negative.")
            .OverridePropertyName("minRent");

        RuleFor(x => x.MaxRent)
            .GreaterThanOrEqualTo(0).WithMessage("Maximum rent may not be negative.")
            .OverridePropertyName("maxRent");

        // Rule 75. Written against the pair rather than as a cross-reference on one of
        // them, so the message names the relationship rather than one half of it.
        RuleFor(x => x.MinRent)
            .Must((query, min) => min <= query.MaxRent)
            .WithMessage("Minimum rent may not be greater than maximum rent.")
            .When(x => x.MinRent is not null && x.MaxRent is not null)
            .OverridePropertyName("minRent");

        RuleFor(x => x.MaxDistanceKm)
            .InclusiveBetween(0.1, SearchPropertiesQuery.MaxDistanceLimitKm)
            .WithMessage(
                $"Maximum distance must be between 0.1 and {SearchPropertiesQuery.MaxDistanceLimitKm} km.")
            .When(x => x.MaxDistanceKm is not null)
            .OverridePropertyName("maxDistanceKm");

        // Rule 74, both halves: a distance with nothing to measure from, and a sort
        // by a column the query would have no way to compute.
        RuleFor(x => x.MaxDistanceKm)
            .Must((query, _) => query.LandmarkId is not null)
            .WithMessage("A maximum distance needs a landmarkId to measure from.")
            .When(x => x.MaxDistanceKm is not null)
            .OverridePropertyName("maxDistanceKm");

        RuleFor(x => x.Sort)
            .Must((query, _) => query.LandmarkId is not null)
            .WithMessage("Sorting by distance needs a landmarkId to measure from.")
            .When(x => string.Equals(x.Sort, nameof(PropertySort.Distance), StringComparison.OrdinalIgnoreCase))
            .OverridePropertyName("sort");
    }
}
