using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Services;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class OtpServiceTests
{
    private readonly IOtpRepository _codes = Substitute.For<IOtpRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly OtpService _service;

    private static readonly OtpSettings Settings = new()
    {
        CodeLength = 6,
        ExpiryMinutes = 10,
        MaxVerifyAttempts = 5,
        MaxRequestsPerWindow = 3,
        RequestWindowMinutes = 10,
        PasswordResetExpiryMinutes = 30
    };

    public OtpServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(call => "hashed:" + call.Arg<string>());
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(1L);

        _service = new OtpService(
            _codes, _cache, _email, _hasher, _unitOfWork,
            Options.Create(Settings), NullLogger<OtpService>.Instance);
    }

    [Fact]
    public async Task Rule_6_issues_a_six_digit_code_that_expires_in_ten_minutes()
    {
        OtpCode? saved = null;
        await _codes.CreateAsync(Arg.Do<OtpCode>(c => saved = c));

        string? emailed = null;
        await _email.SendOtpAsync(Arg.Any<AppUser>(), Arg.Do<string>(c => emailed = c), Arg.Any<OtpPurpose>());

        await _service.IssueAsync(TestData.User(), OtpPurpose.VerifyEmail);

        emailed.ShouldNotBeNull();
        emailed!.Length.ShouldBe(6);
        emailed.ShouldAllBe(c => char.IsDigit(c));

        saved.ShouldNotBeNull();
        saved!.ExpiresAt.ShouldBeInRange(
            DateTime.UtcNow.AddMinutes(9), DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task The_code_is_stored_hashed_and_never_in_clear_text()
    {
        OtpCode? saved = null;
        await _codes.CreateAsync(Arg.Do<OtpCode>(c => saved = c));

        string? emailed = null;
        await _email.SendOtpAsync(Arg.Any<AppUser>(), Arg.Do<string>(c => emailed = c), Arg.Any<OtpPurpose>());

        await _service.IssueAsync(TestData.User(), OtpPurpose.VerifyEmail);

        saved!.CodeHash.ShouldBe("hashed:" + emailed);
        saved.CodeHash.ShouldNotBe(emailed);
    }

    [Fact]
    public async Task Any_code_still_outstanding_is_burned_before_a_new_one_is_issued()
    {
        await _service.IssueAsync(TestData.User(id: 7), OtpPurpose.VerifyEmail);

        await _codes.Received(1).InvalidateActiveAsync(7, OtpPurpose.VerifyEmail);
    }

    [Fact]
    public async Task A_reset_token_expires_in_thirty_minutes_and_goes_out_as_a_link()
    {
        OtpCode? saved = null;
        await _codes.CreateAsync(Arg.Do<OtpCode>(c => saved = c));

        await _service.IssueAsync(TestData.User(), OtpPurpose.ResetPassword);

        saved!.ExpiresAt.ShouldBeInRange(
            DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));

        await _email.Received(1).SendPasswordResetAsync(Arg.Any<AppUser>(), Arg.Any<string>());
        await _email.DidNotReceive().SendOtpAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<OtpPurpose>());
    }

    [Fact]
    public async Task A_reset_token_is_not_a_six_digit_code()
    {
        string? emailed = null;
        await _email.SendPasswordResetAsync(Arg.Any<AppUser>(), Arg.Do<string>(t => emailed = t));

        await _service.IssueAsync(TestData.User(), OtpPurpose.ResetPassword);

        emailed!.Length.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task Rule_7_a_fourth_request_inside_the_window_is_rate_limited()
    {
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(4L);

        var exception = await Should.ThrowAsync<RateLimitException>(
            () => _service.IssueAsync(TestData.User(), OtpPurpose.VerifyEmail));

        exception.RetryAfterSeconds.ShouldBe(600);
        await _codes.DidNotReceive().CreateAsync(Arg.Any<OtpCode>());
        await _email.DidNotReceive().SendOtpAsync(Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<OtpPurpose>());
    }

    [Fact]
    public async Task Rule_7_the_third_request_inside_the_window_still_succeeds()
    {
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(3L);

        await _service.IssueAsync(TestData.User(), OtpPurpose.VerifyEmail);

        await _codes.Received(1).CreateAsync(Arg.Any<OtpCode>());
    }

    [Fact]
    public async Task The_counter_is_keyed_on_a_lower_cased_email_over_the_configured_window()
    {
        await _service.IssueAsync(TestData.User(email: "Mixed@RoomRaah.local"), OtpPurpose.VerifyEmail);

        await _cache.Received(1).IncrementAsync(
            "otp:count:mixed@roomraah.local", TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Validation_fails_when_no_code_is_outstanding()
    {
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>()).Returns((OtpCode?)null);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "123456"));
    }

    [Fact]
    public async Task Rule_6_an_expired_code_is_refused()
    {
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>())
            .Returns(TestData.Otp(minutesUntilExpiry: -1));

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "123456"));

        exception.Message.ShouldContain("expired");
    }

    [Fact]
    public async Task Rule_6_a_code_is_burned_after_five_failed_attempts()
    {
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>())
            .Returns(TestData.Otp(attemptCount: 5));
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "123456"));

        exception.Message.ShouldContain("too many times");
    }

    [Fact]
    public async Task A_wrong_code_counts_as_an_attempt_and_is_persisted()
    {
        var otp = TestData.Otp(attemptCount: 2);
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>()).Returns(otp);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "000000"));

        otp.AttemptCount.ShouldBe(3);
        await _codes.Received(1).UpdateAsync(otp);
        await _unitOfWork.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task A_correct_code_is_returned_without_counting_an_attempt()
    {
        var otp = TestData.Otp();
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>()).Returns(otp);
        _hasher.Verify(otp.CodeHash, "123456").Returns(true);

        var result = await _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "123456");

        result.ShouldBeSameAs(otp);
        otp.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_wrong_code_and_an_expired_code_give_the_same_message()
    {
        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>()).Returns(TestData.Otp());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var wrong = await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "000000"));

        _codes.GetLatestActiveAsync(Arg.Any<int>(), Arg.Any<OtpPurpose>())
            .Returns(TestData.Otp(minutesUntilExpiry: -1));

        var expired = await Should.ThrowAsync<BusinessRuleException>(
            () => _service.ValidateAsync(TestData.User(), OtpPurpose.VerifyEmail, "000000"));

        wrong.Message.ShouldBe(expired.Message);
    }
}
