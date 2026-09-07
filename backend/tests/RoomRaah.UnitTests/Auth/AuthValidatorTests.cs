using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Auth.Commands;

namespace RoomRaah.UnitTests.Auth;

public class VerifyOtpCommandValidatorTests
{
    private readonly VerifyOtpCommandValidator _validator = new();

    private static VerifyOtpCommand Valid() => new()
    {
        Email = "seeker@roomraah.local",
        Code = "123456"
    };

    [Fact]
    public void Accepts_an_email_and_a_six_digit_code()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Email_must_be_a_valid_address(string email)
    {
        var command = Valid();
        command.Email = email;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("12 456")]
    public void Rule_6_the_code_must_be_exactly_six_digits(string code)
    {
        var command = Valid();
        command.Code = code;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Code);
    }
}

public class ResendOtpCommandValidatorTests
{
    private readonly ResendOtpCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_email()
    {
        _validator.TestValidate(new ResendOtpCommand { Email = "a@b.com" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    public void Refuses_anything_that_is_not_an_email(string email)
    {
        _validator.TestValidate(new ResendOtpCommand { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class RefreshCommandValidatorTests
{
    private readonly RefreshCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_token()
    {
        _validator.TestValidate(new RefreshCommand { RefreshToken = "token" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void The_token_is_required()
    {
        _validator.TestValidate(new RefreshCommand { RefreshToken = "" })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}

public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_token()
    {
        _validator.TestValidate(new LogoutCommand { RefreshToken = "token", AppUserId = 1 })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void The_token_is_required()
    {
        _validator.TestValidate(new LogoutCommand { RefreshToken = "", AppUserId = 1 })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}

public class ForgotPasswordCommandValidatorTests
{
    private readonly ForgotPasswordCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_email()
    {
        _validator.TestValidate(new ForgotPasswordCommand { Email = "a@b.com" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    public void Refuses_anything_that_is_not_an_email(string email)
    {
        _validator.TestValidate(new ForgotPasswordCommand { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    private static ResetPasswordCommand Valid() => new()
    {
        Email = "seeker@roomraah.local",
        Token = "reset-token",
        NewPassword = "Karachi2026"
    };

    [Fact]
    public void Accepts_a_complete_reset()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void The_token_is_required()
    {
        var command = Valid();
        command.Token = "";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Token);
    }

    [Theory]
    [InlineData("Short1A")]
    [InlineData("password1")]
    [InlineData("PASSWORD")]
    [InlineData("Password")]
    public void Rule_2_the_new_password_obeys_the_same_rules_as_registration(string password)
    {
        var command = Valid();
        command.NewPassword = password;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
