using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class VerifyOtpCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOtpRepository _codes = Substitute.For<IOtpRepository>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly VerifyOtpCommandHandler _handler;

    public VerifyOtpCommandHandlerTests()
    {
        _handler = new VerifyOtpCommandHandler(
            _users, _codes, _otp, _sessions, _unitOfWork,
            NullLogger<VerifyOtpCommandHandler>.Instance);
    }

    private static VerifyOtpCommand Command() => new()
    {
        Email = "seeker@roomraah.local",
        Code = "123456"
    };

    private AppUser ArrangeUnverifiedUser()
    {
        var user = TestData.User(isEmailVerified: false);
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.VerifyEmail, "123456").Returns(TestData.Otp());
        _sessions.IssueAsync(user).Returns(new AuthResponseDto { AccessToken = "jwt" });
        return user;
    }

    [Fact]
    public async Task A_correct_code_marks_the_account_verified()
    {
        var user = ArrangeUnverifiedUser();

        await _handler.Handle(Command(), CancellationToken.None);

        user.IsEmailVerified.ShouldBeTrue();
        await _users.Received(1).UpdateAsync(user);
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_6_the_code_is_consumed_so_it_cannot_be_used_twice()
    {
        var user = TestData.User(isEmailVerified: false);
        var otp = TestData.Otp();
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.VerifyEmail, "123456").Returns(otp);
        _sessions.IssueAsync(user).Returns(new AuthResponseDto());

        await _handler.Handle(Command(), CancellationToken.None);

        otp.ConsumedAt.ShouldNotBeNull();
        await _codes.Received(1).UpdateAsync(otp);
    }

    [Fact]
    public async Task Verification_returns_a_fresh_session()
    {
        ArrangeUnverifiedUser();

        var result = await _handler.Handle(Command(), CancellationToken.None);

        result.AccessToken.ShouldBe("jwt");
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
    }

    [Fact]
    public async Task A_deactivated_account_is_refused()
    {
        _users.GetByEmailAsync(Arg.Any<string>())
            .Returns(TestData.User(isActive: false, isEmailVerified: false));

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldContain("deactivated");
    }

    [Fact]
    public async Task A_rejected_code_leaves_the_account_unverified()
    {
        var user = TestData.User(isEmailVerified: false);
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.VerifyEmail, "123456")
            .Returns<OtpCode>(_ => throw new BusinessRuleException("bad code"));

        await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        user.IsEmailVerified.ShouldBeFalse();
        await _sessions.DidNotReceive().IssueAsync(Arg.Any<AppUser>());
    }
}
