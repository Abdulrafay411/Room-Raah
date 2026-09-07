using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Properties.Commands;

namespace RoomRaah.UnitTests.Properties;

public class CreatePropertyCommandValidatorTests
{
    private readonly CreatePropertyCommandValidator _validator = new();

    private static CreatePropertyCommand Valid() => new()
    {
        AreaId = 3,
        Title = "Bright shared room near Main Boulevard",
        Description = "A four-bed shared room in a well-kept portion.",
        AddressLine = "House 42, Block C, Gulberg III",
        Latitude = 31.5204,
        Longitude = 74.3587,
        RoomType = "Shared",
        GenderPolicy = "Boys",
        TotalBeds = 4,
        AvailableBeds = 2,
        MonthlyRent = 18000m
    };

    [Fact]
    public void A_complete_listing_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Title_is_required(string title)
    {
        var command = Valid();
        command.Title = title;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_stops_at_150_characters()
    {
        var command = Valid();
        command.Title = new string('a', 151);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Description_is_required()
    {
        var command = Valid();
        command.Description = "";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_stops_at_4000_characters()
    {
        var command = Valid();
        command.Description = new string('a', 4001);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Address_is_required()
    {
        var command = Valid();
        command.AddressLine = "";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AddressLine);
    }

    [Fact]
    public void Address_stops_at_300_characters()
    {
        var command = Valid();
        command.AddressLine = new string('a', 301);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AddressLine);
    }

    /// <summary>
    /// Rule 17. A missing coordinate is the case that matters: on a non-nullable
    /// double it would arrive as 0, which is a real position off the coast of
    /// Africa and would pass every range check there is.
    /// </summary>
    [Fact]
    public void Rule_17_a_listing_with_no_pin_is_refused()
    {
        var command = Valid();
        command.Latitude = null;
        command.Longitude = null;

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Latitude);
        result.ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Fact]
    public void A_pin_on_the_equator_and_the_prime_meridian_is_accepted()
    {
        var command = Valid();
        command.Latitude = 0;
        command.Longitude = 0;

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Latitude);
        result.ShouldNotHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Latitude_stays_within_the_globe(double latitude)
    {
        var command = Valid();
        command.Latitude = latitude;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Longitude_stays_within_the_globe(double longitude)
    {
        var command = Valid();
        command.Longitude = longitude;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Studio")]
    public void Room_type_must_be_one_of_the_two(string roomType)
    {
        var command = Valid();
        command.RoomType = roomType;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RoomType);
    }

    [Theory]
    [InlineData("Private")]
    [InlineData("shared")]
    public void Room_type_is_read_without_regard_to_case(string roomType)
    {
        var command = Valid();
        command.RoomType = roomType;

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.RoomType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Couples")]
    public void Gender_policy_must_be_one_of_the_three(string policy)
    {
        var command = Valid();
        command.GenderPolicy = policy;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.GenderPolicy);
    }

    [Fact]
    public void Rule_16_total_beds_is_required()
    {
        var command = Valid();
        command.TotalBeds = null;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.TotalBeds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Rule_16_total_beds_runs_from_one_to_a_hundred(int beds)
    {
        var command = Valid();
        command.TotalBeds = beds;
        command.AvailableBeds = 0;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.TotalBeds);
    }

    [Fact]
    public void Rule_16_available_beds_may_not_be_negative()
    {
        var command = Valid();
        command.AvailableBeds = -1;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AvailableBeds);
    }

    [Fact]
    public void Rule_16_available_beds_may_not_exceed_total_beds()
    {
        var command = Valid();
        command.TotalBeds = 4;
        command.AvailableBeds = 5;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor("AvailableBeds");
    }

    [Fact]
    public void A_full_room_reports_no_beds_free()
    {
        var command = Valid();
        command.TotalBeds = 4;
        command.AvailableBeds = 0;

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Every_bed_free_is_accepted()
    {
        var command = Valid();
        command.TotalBeds = 4;
        command.AvailableBeds = 4;

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Monthly_rent_is_required()
    {
        var command = Valid();
        command.MonthlyRent = null;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.MonthlyRent);
    }

    [Fact]
    public void Monthly_rent_of_nothing_is_refused()
    {
        var command = Valid();
        command.MonthlyRent = 0m;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.MonthlyRent);
    }

    [Fact]
    public void A_deposit_of_zero_is_accepted_but_a_negative_one_is_not()
    {
        var free = Valid();
        free.SecurityDeposit = 0m;
        _validator.TestValidate(free).ShouldNotHaveValidationErrorFor(x => x.SecurityDeposit);

        var negative = Valid();
        negative.SecurityDeposit = -1m;
        _validator.TestValidate(negative).ShouldHaveValidationErrorFor(x => x.SecurityDeposit);
    }

    [Fact]
    public void Utilities_and_mess_charges_may_not_be_negative()
    {
        var command = Valid();
        command.UtilitiesCharge = -1m;
        command.MessCharge = -1m;

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UtilitiesCharge);
        result.ShouldHaveValidationErrorFor(x => x.MessCharge);
    }

    [Fact]
    public void House_rules_are_optional_and_stop_at_2000_characters()
    {
        var none = Valid();
        none.HouseRules = null;
        _validator.TestValidate(none).ShouldNotHaveValidationErrorFor(x => x.HouseRules);

        var tooLong = Valid();
        tooLong.HouseRules = new string('a', 2001);
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.HouseRules);
    }

    [Fact]
    public void An_area_must_be_chosen()
    {
        var command = Valid();
        command.AreaId = 0;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AreaId);
    }
}
