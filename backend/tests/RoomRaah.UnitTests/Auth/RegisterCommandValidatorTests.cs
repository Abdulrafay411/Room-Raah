using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Auth.Commands;

namespace RoomRaah.UnitTests.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand Valid() => new()
    {
        FullName = "Hamza Iqbal",
        Email = "hamza@roomraah.local",
        PhoneNumber = "+923001112233",
        Password = "Password1",
        Role = "Seeker"
    };

    [Fact]
    public void Accepts_a_complete_seeker_registration()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Accepts_a_complete_owner_registration()
    {
        var command = Valid();
        command.Role = "Owner";
        command.CnicNumber = "3520112345671";

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Rule_3_full_name_is_required(string? fullName)
    {
        var command = Valid();
        command.FullName = fullName!;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Rule_3_full_name_stops_at_120_characters()
    {
        var command = Valid();
        command.FullName = new string('a', 121);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    public void Email_must_be_a_valid_address(string email)
    {
        var command = Valid();
        command.Email = email;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("Short1A")]
    [InlineData("password1")]
    [InlineData("PASSWORD")]
    [InlineData("Password")]
    public void Rule_2_password_needs_length_an_uppercase_and_a_digit(string password)
    {
        var command = Valid();
        command.Password = password;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Rule_2_accepts_a_password_that_satisfies_every_part()
    {
        var command = Valid();
        command.Password = "Karachi2026";

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("0300 111 2233")]
    [InlineData("+92-300-1112233")]
    [InlineData("abc")]
    public void Phone_number_allows_digits_and_a_leading_plus_only(string phoneNumber)
    {
        var command = Valid();
        command.PhoneNumber = phoneNumber;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void An_admin_account_cannot_be_self_registered()
    {
        var command = Valid();
        command.Role = "Admin";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void An_unknown_role_is_refused()
    {
        var command = Valid();
        command.Role = "Landlord";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void An_owner_must_supply_a_cnic()
    {
        var command = Valid();
        command.Role = "Owner";
        command.CnicNumber = null;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CnicNumber);
    }

    [Theory]
    [InlineData("35201")]
    [InlineData("35201-1234567-1")]
    [InlineData("35201123456712")]
    public void A_cnic_must_be_exactly_thirteen_digits(string cnic)
    {
        var command = Valid();
        command.Role = "Owner";
        command.CnicNumber = cnic;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.CnicNumber);
    }

    [Fact]
    public void A_seeker_is_not_asked_for_a_cnic()
    {
        var command = Valid();
        command.CnicNumber = null;

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.CnicNumber);
    }
}
