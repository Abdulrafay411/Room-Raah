using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _users, _hasher, _sessions, NullLogger<LoginCommandHandler>.Instance);
    }

    private static LoginCommand Command(string email = "Seeker@RoomRaah.local") => new()
    {
        Email = email,
        Password = "Password1"
    };

    [Fact]
    public async Task Returns_a_session_for_correct_credentials()
    {
        var user = TestData.User();
        _users.GetByEmailAsync("seeker@roomraah.local").Returns(user);
        _hasher.Verify(user.PasswordHash, "Password1").Returns(true);
        _sessions.IssueAsync(user).Returns(new AuthResponseDto { AccessToken = "jwt" });

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.AccessToken.ShouldBe("jwt");
    }

    [Fact]
    public async Task Looks_the_account_up_by_a_lower_cased_email()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command("  MiXeD@RoomRaah.local  "), CancellationToken.None));

        await _users.Received(1).GetByEmailAsync("mixed@roomraah.local");
    }

    [Fact]
    public async Task An_unknown_email_is_rejected_as_bad_credentials()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        var exception = await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task A_wrong_password_gives_the_same_message_as_an_unknown_email()
    {
        var user = TestData.User();
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var exception = await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Rule_11_a_deactivated_account_cannot_log_in()
    {
        var user = TestData.User(isActive: false);
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldContain("deactivated");
    }

    [Fact]
    public async Task Rule_5_an_unverified_account_may_still_log_in()
    {
        var user = TestData.User(isEmailVerified: false);
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _sessions.IssueAsync(user).Returns(new AuthResponseDto { AccessToken = "jwt" });

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.AccessToken.ShouldBe("jwt");
    }

    [Fact]
    public async Task No_session_is_issued_when_the_password_is_wrong()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(TestData.User());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        await _sessions.DidNotReceive().IssueAsync(Arg.Any<AppUser>());
    }
}
