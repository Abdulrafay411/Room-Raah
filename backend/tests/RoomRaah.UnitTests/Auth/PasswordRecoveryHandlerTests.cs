using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _handler = new ForgotPasswordCommandHandler(_users, _otp);
    }

    private static ForgotPasswordCommand Command(string email = "seeker@roomraah.local") =>
        new() { Email = email };

    [Fact]
    public async Task A_known_account_is_sent_a_reset_token()
    {
        var user = TestData.User();
        _users.GetByEmailAsync(user.Email).Returns(user);

        await _handler.Handle(Command(), CancellationToken.None);

        await _otp.Received(1).IssueAsync(user, OtpPurpose.ResetPassword);
    }

    [Fact]
    public async Task An_unknown_email_succeeds_quietly_so_accounts_cannot_be_enumerated()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        await Should.NotThrowAsync(() => _handler.Handle(Command(), CancellationToken.None));

        await _otp.DidNotReceive().IssueAsync(Arg.Any<AppUser>(), Arg.Any<OtpPurpose>());
    }

    [Fact]
    public async Task A_deactivated_account_is_sent_nothing()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(TestData.User(isActive: false));

        await _handler.Handle(Command(), CancellationToken.None);

        await _otp.DidNotReceive().IssueAsync(Arg.Any<AppUser>(), Arg.Any<OtpPurpose>());
    }
}

public class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IOtpRepository _codes = Substitute.For<IOtpRepository>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("new-hash");

        _handler = new ResetPasswordCommandHandler(
            _users, _codes, _otp, _tokens, _hasher, _unitOfWork,
            NullLogger<ResetPasswordCommandHandler>.Instance);
    }

    private static ResetPasswordCommand Command() => new()
    {
        Email = "seeker@roomraah.local",
        Token = "reset-token",
        NewPassword = "Karachi2026"
    };

    private AppUser ArrangeValidReset()
    {
        var user = TestData.User();
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.ResetPassword, "reset-token")
            .Returns(TestData.Otp(purpose: OtpPurpose.ResetPassword, minutesUntilExpiry: 30));
        return user;
    }

    [Fact]
    public async Task The_password_hash_is_replaced()
    {
        var user = ArrangeValidReset();

        await _handler.Handle(Command(), CancellationToken.None);

        user.PasswordHash.ShouldBe("new-hash");
        await _users.Received(1).UpdateAsync(user);
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_12_the_token_is_consumed_so_the_link_works_only_once()
    {
        var user = TestData.User();
        var otp = TestData.Otp(purpose: OtpPurpose.ResetPassword);
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.ResetPassword, "reset-token").Returns(otp);

        await _handler.Handle(Command(), CancellationToken.None);

        otp.ConsumedAt.ShouldNotBeNull();
        await _codes.Received(1).UpdateAsync(otp);
    }

    [Fact]
    public async Task Every_other_session_is_ended()
    {
        var user = ArrangeValidReset();

        await _handler.Handle(Command(), CancellationToken.None);

        await _tokens.Received(1).RevokeAllForUserAsync(user.Id);
    }

    [Fact]
    public async Task An_unknown_email_looks_the_same_as_an_expired_link()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((AppUser?)null);

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        exception.Message.ShouldContain("invalid or has expired");
    }

    [Fact]
    public async Task A_deactivated_account_cannot_reset_its_password()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(TestData.User(isActive: false));

        await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        await _users.DidNotReceive().UpdateAsync(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task A_rejected_token_leaves_the_password_untouched()
    {
        var user = TestData.User();
        _users.GetByEmailAsync(user.Email).Returns(user);
        _otp.ValidateAsync(user, OtpPurpose.ResetPassword, "reset-token")
            .Returns<OtpCode>(_ => throw new BusinessRuleException("bad token"));

        await Should.ThrowAsync<BusinessRuleException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        user.PasswordHash.ShouldBe("hashed");
        await _tokens.DidNotReceive().RevokeAllForUserAsync(Arg.Any<int>());
    }
}
