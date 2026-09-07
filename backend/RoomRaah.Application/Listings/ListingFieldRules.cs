using FluentValidation;
using RoomRaah.Application.Common;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Listings;

/// <summary>
/// Every field rule the Field Validation table lists for a listing, in one place.
/// The create and update validators inherit it and add only what is theirs.
/// </summary>
public class ListingFieldRules<T> : AbstractValidator<T> where T : IListingFields
{
    public ListingFieldRules()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(150).WithMessage("Title must not exceed 150 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000).WithMessage("Description must not exceed 4000 characters.");

        RuleFor(x => x.AddressLine)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.Latitude)
            .NotNull().WithMessage("Latitude is required. Drop a pin on the map.")
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .NotNull().WithMessage("Longitude is required. Drop a pin on the map.")
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.RoomType)
            .NotEmpty().WithMessage("Room type is required.")
            .Must(BeARoomType).WithMessage("Room type must be either Private or Shared.");

        RuleFor(x => x.GenderPolicy)
            .NotEmpty().WithMessage("Gender policy is required.")
            .Must(BeAGenderPolicy).WithMessage("Gender policy must be Boys, Girls or Family.");

        RuleFor(x => x.TotalBeds)
            .NotNull().WithMessage("Total beds is required.")
            .InclusiveBetween(1, 100).WithMessage("Total beds must be between 1 and 100.");

        RuleFor(x => x.AvailableBeds)
            .NotNull().WithMessage("Available beds is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Available beds cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.AvailableBeds <= x.TotalBeds)
            .WithMessage("Available beds cannot exceed total beds.")
            .OverridePropertyName("AvailableBeds")
            .When(x => x.AvailableBeds.HasValue && x.TotalBeds.HasValue);

        RuleFor(x => x.MonthlyRent)
            .NotNull().WithMessage("Monthly rent is required.")
            .GreaterThan(0).WithMessage("Monthly rent must be greater than 0.");

        RuleFor(x => x.SecurityDeposit)
            .GreaterThanOrEqualTo(0).WithMessage("Security deposit cannot be negative.");

        RuleFor(x => x.UtilitiesCharge)
            .GreaterThanOrEqualTo(0).WithMessage("Utilities charge cannot be negative.");

        RuleFor(x => x.MessCharge)
            .GreaterThanOrEqualTo(0).WithMessage("Mess charge cannot be negative.");

        RuleFor(x => x.HouseRules)
            .MaximumLength(2000).WithMessage("House rules must not exceed 2000 characters.");

        RuleFor(x => x.AreaId)
            .GreaterThan(0).WithMessage("Area is required.");
    }

    private static bool BeARoomType(string value) => EnumName.IsDefined<RoomType>(value);

    private static bool BeAGenderPolicy(string value) => EnumName.IsDefined<GenderPolicy>(value);
}
