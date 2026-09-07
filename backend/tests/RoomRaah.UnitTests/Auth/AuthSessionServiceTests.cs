using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Services;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class AuthSessionServiceTests
{
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuthSessionService _service;

    private static readonly JwtSettings Settings = new()
    {
        Key = "roomraah-unit-test-signing-key-at-least-32-characters",
        Issuer = "RoomRaah.API",
        Audience = "RoomRaah.Client",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7
    };

    public AuthSessionServiceTests()
    {
        _tokenService.GenerateAccessToken(Arg.Any<AppUser>()).Returns("access-token");
        _tokenService.GenerateRefreshToken().Returns("refresh-token");

        _service = new AuthSessionService(
            _tokenService, _tokens, _unitOfWork, TestMapper.Create(), Options.Create(Settings));
    }

    [Fact]
    public async Task Returns_both_tokens_and_the_account_summary()
    {
        var user = TestData.User(id: 42, email: "owner@roomraah.local", role: UserRole.Owner);

        var result = await _service.IssueAsync(user);

        result.AccessToken.ShouldBe("access-token");
        result.RefreshToken.ShouldBe("refresh-token");
        result.User.Id.ShouldBe(42);
        result.User.Email.ShouldBe("owner@roomraah.local");
        result.User.Role.ShouldBe("Owner");
    }

    [Fact]
    public async Task Rule_8_the_access_token_lifetime_is_reported_in_seconds()
    {
        var result = await _service.IssueAsync(TestData.User());

        result.ExpiresInSeconds.ShouldBe(1800);
    }

    [Fact]
    public async Task Rule_8_the_refresh_token_is_persisted_with_a_seven_day_expiry()
    {
        RefreshToken? saved = null;
        await _tokens.CreateAsync(Arg.Do<RefreshToken>(t => saved = t));

        await _service.IssueAsync(TestData.User(id: 42));

        saved.ShouldNotBeNull();
        saved!.AppUserId.ShouldBe(42);
        saved.Token.ShouldBe("refresh-token");
        saved.RevokedAt.ShouldBeNull();
        saved.ExpiresAt.ShouldBeInRange(
            DateTime.UtcNow.AddDays(6.9), DateTime.UtcNow.AddDays(7.1));

        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task The_summary_carries_no_phone_number()
    {
        var result = await _service.IssueAsync(TestData.User());

        typeof(RoomRaah.Application.DTOs.UserSummaryDto)
            .GetProperties()
            .ShouldNotContain(p => p.Name.Contains("Phone", StringComparison.OrdinalIgnoreCase));

        result.User.ShouldNotBeNull();
    }
}
