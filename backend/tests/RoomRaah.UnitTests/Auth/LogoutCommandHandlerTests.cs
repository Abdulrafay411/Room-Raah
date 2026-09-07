using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(
            _tokens, _tokenService, _unitOfWork, NullLogger<LogoutCommandHandler>.Instance);
    }

    private static LogoutCommand Command(int appUserId = 1) => new()
    {
        RefreshToken = "refresh-token",
        AppUserId = appUserId
    };

    [Fact]
    public async Task Rule_10_the_token_is_added_to_the_revocation_set_for_its_remaining_lifetime()
    {
        _tokens.GetByTokenAsync("refresh-token").Returns(TestData.Token(daysUntilExpiry: 3));

        await _handler.Handle(Command(), CancellationToken.None);

        await _tokenService.Received(1).RevokeRefreshTokenAsync(
            "refresh-token",
            Arg.Is<TimeSpan>(t => t.TotalDays > 2.9 && t.TotalDays <= 3));
    }

    [Fact]
    public async Task Rule_10_the_database_row_is_stamped_as_revoked()
    {
        var stored = TestData.Token();
        _tokens.GetByTokenAsync("refresh-token").Returns(stored);

        await _handler.Handle(Command(), CancellationToken.None);

        stored.RevokedAt.ShouldNotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task An_unknown_token_is_not_found()
    {
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(Command(), CancellationToken.None));
    }

    [Fact]
    public async Task Another_persons_token_cannot_be_revoked()
    {
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns(TestData.Token(appUserId: 99));

        await Should.ThrowAsync<NotFoundException>(
            () => _handler.Handle(Command(appUserId: 1), CancellationToken.None));

        await _tokenService.DidNotReceive()
            .RevokeRefreshTokenAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task An_already_expired_token_is_not_written_to_redis()
    {
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns(TestData.Token(daysUntilExpiry: -1));

        await _handler.Handle(Command(), CancellationToken.None);

        await _tokenService.DidNotReceive()
            .RevokeRefreshTokenAsync(Arg.Any<string>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task Logging_out_twice_is_harmless()
    {
        var stored = TestData.Token(revokedAt: DateTime.UtcNow.AddMinutes(-5));
        _tokens.GetByTokenAsync(Arg.Any<string>()).Returns(stored);

        await _handler.Handle(Command(), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }
}
