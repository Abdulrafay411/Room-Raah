using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Authentication;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class TokenServiceTests
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly TokenService _service;

    private static readonly JwtSettings Settings = new()
    {
        Key = "roomraah-unit-test-signing-key-at-least-32-characters",
        Issuer = "RoomRaah.API",
        Audience = "RoomRaah.Client",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7
    };

    public TokenServiceTests()
    {
        _service = new TokenService(Options.Create(Settings), _cache);
    }

    private JwtSecurityToken Decode(AppUser user)
    {
        return new JwtSecurityTokenHandler().ReadJwtToken(_service.GenerateAccessToken(user));
    }

    [Fact]
    public void The_token_carries_the_account_id_email_and_role()
    {
        var token = Decode(TestData.User(id: 42, email: "owner@roomraah.local", role: UserRole.Owner));

        token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.ShouldBe("42");
        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value
            .ShouldBe("owner@roomraah.local");
        token.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Owner");
    }

    [Fact]
    public void Rule_5_the_token_says_whether_the_email_is_verified()
    {
        Decode(TestData.User(isEmailVerified: true)).Claims
            .First(c => c.Type == TokenService.EmailVerifiedClaim).Value.ShouldBe("true");

        Decode(TestData.User(isEmailVerified: false)).Claims
            .First(c => c.Type == TokenService.EmailVerifiedClaim).Value.ShouldBe("false");
    }

    [Fact]
    public void Rule_8_the_access_token_expires_in_thirty_minutes()
    {
        var token = Decode(TestData.User());

        token.ValidTo.ShouldBeInRange(
            DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void The_issuer_and_audience_come_from_configuration()
    {
        var token = Decode(TestData.User());

        token.Issuer.ShouldBe("RoomRaah.API");
        token.Audiences.ShouldContain("RoomRaah.Client");
    }

    [Fact]
    public void Someone_holding_two_profiles_gets_a_role_claim_for_each()
    {
        var user = TestData.User(role: UserRole.Owner);
        user.OwnerProfile = new OwnerProfile();
        user.SeekerProfile = new SeekerProfile();

        var roles = Decode(user).Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        roles.ShouldContain("Owner");
        roles.ShouldContain("Seeker");
        roles.Count.ShouldBe(2);
    }

    [Fact]
    public void The_token_never_carries_the_password_hash_or_the_phone_number()
    {
        var user = TestData.User();
        var raw = _service.GenerateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);

        token.Claims.ShouldNotContain(c => c.Value == user.PasswordHash);
        token.Claims.ShouldNotContain(c => c.Value == user.PhoneNumber);
    }

    [Fact]
    public void Two_refresh_tokens_are_never_the_same()
    {
        var first = _service.GenerateRefreshToken();
        var second = _service.GenerateRefreshToken();

        first.ShouldNotBe(second);
        Convert.FromBase64String(first).Length.ShouldBe(64);
    }

    [Fact]
    public async Task Rule_10_revoking_writes_an_entry_for_the_tokens_remaining_lifetime()
    {
        await _service.RevokeRefreshTokenAsync("some-token", TimeSpan.FromDays(3));

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.StartsWith("revoked:")), "1", TimeSpan.FromDays(3));
    }

    [Fact]
    public async Task The_revocation_key_never_contains_the_token_itself()
    {
        var token = _service.GenerateRefreshToken();

        string? key = null;
        await _cache.SetAsync(Arg.Do<string>(k => key = k), Arg.Any<string>(), Arg.Any<TimeSpan>());

        await _service.RevokeRefreshTokenAsync(token, TimeSpan.FromDays(3));

        key.ShouldNotBeNull();
        key!.ShouldNotContain(token);
        key.ShouldBe("revoked:" + key["revoked:".Length..]);
        key["revoked:".Length..].Length.ShouldBe(64);
    }

    [Fact]
    public async Task Rule_9_a_token_in_the_revocation_set_is_reported_as_revoked()
    {
        string? writtenKey = null;
        await _cache.SetAsync(Arg.Do<string>(k => writtenKey = k), Arg.Any<string>(), Arg.Any<TimeSpan>());

        await _service.RevokeRefreshTokenAsync("some-token", TimeSpan.FromDays(3));

        _cache.GetAsync<string>(writtenKey!).Returns("1");

        (await _service.IsRefreshTokenRevoked("some-token")).ShouldBeTrue();
    }

    [Fact]
    public async Task A_different_token_does_not_collide_with_a_revoked_one()
    {
        string? writtenKey = null;
        await _cache.SetAsync(Arg.Do<string>(k => writtenKey = k), Arg.Any<string>(), Arg.Any<TimeSpan>());

        await _service.RevokeRefreshTokenAsync("some-token", TimeSpan.FromDays(3));

        _cache.GetAsync<string>(writtenKey!).Returns("1");
        _cache.GetAsync<string>(Arg.Is<string>(k => k != writtenKey)).Returns((string?)null);

        (await _service.IsRefreshTokenRevoked("a-different-token")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_token_absent_from_the_revocation_set_is_not_revoked()
    {
        _cache.GetAsync<string>(Arg.Any<string>()).Returns((string?)null);

        (await _service.IsRefreshTokenRevoked("some-token")).ShouldBeFalse();
    }
}
