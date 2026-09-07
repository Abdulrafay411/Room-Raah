using NSubstitute;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class ResendOtpCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();
    private readonly ResendOtpCommandHandler _handler;

    public ResendOtpCommandHandlerTests()
    {
        _handler = new ResendOtpCommandHandler(_users, _otp);
    }

    private static ResendOtpCommand Command() => new() { Email = "seeker@roomraah.local" };

    [Fact]
    public async Task An_unverified_account_gets_a_new_code()
    {
        var user = TestData.User(isEmailVerified: false);
        _users.GetByEmailAsync(user.Email).Returns(user);

        await _handler.Handle(Command(), CancellationToken.None);

        await _otp.Received(1).IssueAsync(user, OtpPurpose.VerifyEmail);
    }

    [Fact]
    public async Task An_unknown_email_is_not_found()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task An_already_verified_account_is_refused()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(TestData.User(isEmailVerified: true));

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldContain("already been verified");
        await _otp.DidNotReceive().IssueAsync(Arg.Any<AppUser>(), Arg.Any<OtpPurpose>());
    }

    [Fact]
    public async Task A_deactivated_account_is_refused()
    {
        _users.GetByEmailAsync(Arg.Any<string>())
            .Returns(TestData.User(isActive: false, isEmailVerified: false));

        await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        await _otp.DidNotReceive().IssueAsync(Arg.Any<AppUser>(), Arg.Any<OtpPurpose>());
    }

    [Fact]
    public async Task Rule_7_a_rate_limit_reaches_the_caller_unchanged()
    {
        var user = TestData.User(isEmailVerified: false);
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.IssueAsync(user, OtpPurpose.VerifyEmail)
            .Returns<Task>(_ => throw new RateLimitException("too many", 600));

        var exception = await Should.ThrowAsync<RateLimitException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.RetryAfterSeconds.ShouldBe(600);
    }
}
