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

public class RefreshCommandHandlerTests
{
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuthSessionService _sessions = Substitute.For<IAuthSessionService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RefreshCommandHandler _handler;

    public RefreshCommandHandlerTests()
    {
        _handler = new RefreshCommandHandler(
            _tokens, _users, _tokenService, _sessions, _unitOfWork,
            NullLogger<RefreshCommandHandler>.Instance);
    }

    private static RefreshCommand Command() => new() { RefreshToken = "refresh-token" };

    private RefreshToken ArrangeValidToken()
    {
        var stored = TestData.Token();
        var user = TestData.User();
        _tokenService.IsRefreshTokenRevoked("refresh-token").Returns(false);
        _tokens.GetByTokenAsync("refresh-token").Returns(stored);
        _users.GetByIdAsync(stored.AppUserId).Returns(user);
        _sessions.IssueAsync(user).Returns(new AuthResponseDto { RefreshToken = "rotated" });
        return stored;
    }

    [Fact]
    public async Task Rule_9_the_revocation_set_is_checked_before_the_database()
    {
        _tokenService.IsRefreshTokenRevoked("refresh-token").Returns(true);

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        await _tokens.DidNotReceive().GetByTokenAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Rule_9_an_unknown_token_is_rejected()
    {
        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(false);
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Rule_9_a_token_already_revoked_in_the_database_is_rejected()
    {
        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(false);
        _tokens.GetByTokenAsync(Arg.Any<string>())
            .Returns(TestData.Token(revokedAt: DateTime.UtcNow.AddMinutes(-1)));

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Rule_8_an_expired_token_is_rejected()
    {
        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(false);
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns(TestData.Token(daysUntilExpiry: -1));

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task A_deactivated_account_cannot_refresh()
    {
        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(false);
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns(TestData.Token());
        _users.GetByIdAsync(Arg.Any<int>()).Returns(TestData.User(isActive: false));

        await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        await _sessions.DidNotReceive().IssueAsync(Arg.Any<AppUser>());
    }

    [Fact]
    public async Task Rule_9_a_valid_token_is_rotated()
    {
        var stored = ArrangeValidToken();

        var result = await _handler.Handle(Command(), CancellationToken.None);

        stored.RevokedAt.ShouldNotBeNull();
        result.RefreshToken.ShouldBe("rotated");
        await _unitOfWork.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task Every_rejection_gives_the_same_message()
    {
        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(true);
        var revoked = await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        _tokenService.IsRefreshTokenRevoked(Arg.Any<string>()).Returns(false);
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns((RefreshToken?)null);
        var unknown = await Should.ThrowAsync<UnauthenticatedException>(
            () => _handler.Handle(Command(), CancellationToken.None));

        revoked.Message.ShouldBe(unknown.Message);
    }
}
