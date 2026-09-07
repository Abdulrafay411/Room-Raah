using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Auth.Commands;

namespace RoomRaah.UnitTests.Auth;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Accepts_an_email_and_a_password()
    {
        var command = new LoginCommand { Email = "a@b.com", Password = "Password1" };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Email_is_required()
    {
        var command = new LoginCommand { Email = "", Password = "Password1" };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Password_is_required()
    {
        var command = new LoginCommand { Email = "a@b.com", Password = "" };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Password);
    }
}
