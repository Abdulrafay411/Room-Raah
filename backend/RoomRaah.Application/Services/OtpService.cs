using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Services;

public class OtpService : IOtpService
{
    private readonly IOtpRepository _otpRepository;
    private readonly ICacheService _cacheService;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly OtpSettings _otpSettings;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IOtpRepository otpRepository,
        ICacheService cacheService,
        IEmailSender emailSender,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IOptions<OtpSettings> otpSettings,
        ILogger<OtpService> logger)
    {
        _otpRepository = otpRepository;
        _cacheService = cacheService;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _otpSettings = otpSettings.Value;
        _logger = logger;
    }

    public async Task IssueAsync(AppUser user, OtpPurpose purpose)
    {
        var window = TimeSpan.FromMinutes(_otpSettings.RequestWindowMinutes);
        var count = await _cacheService.IncrementAsync($"otp:count:{user.Email.ToLowerInvariant()}", window);

        if (count > _otpSettings.MaxRequestsPerWindow)
        {
            throw new RateLimitException(
                $"Too many codes requested. Try again in {_otpSettings.RequestWindowMinutes} minutes.",
                (int)window.TotalSeconds);
        }

        await _otpRepository.InvalidateActiveAsync(user.Id, purpose);

        var isReset = purpose == OtpPurpose.ResetPassword;
        var secret = isReset ? GenerateResetToken() : GenerateNumericCode(_otpSettings.CodeLength);
        var minutes = isReset ? _otpSettings.PasswordResetExpiryMinutes : _otpSettings.ExpiryMinutes;

        await _otpRepository.CreateAsync(new OtpCode
        {
            AppUserId = user.Id,
            Purpose = purpose,
            CodeHash = _passwordHasher.Hash(secret),
            ExpiresAt = DateTime.UtcNow.AddMinutes(minutes)
        });

        await _unitOfWork.SaveChangesAsync();

        if (isReset)
        {
            await _emailSender.SendPasswordResetAsync(user, secret);
        }
        else
        {
            await _emailSender.SendOtpAsync(user, secret, purpose);
        }

        _logger.LogInformation("OTP sent to user {UserId} for {Purpose}", user.Id, purpose);
    }

    public async Task<OtpCode> ValidateAsync(AppUser user, OtpPurpose purpose, string code)
    {
        var otp = await _otpRepository.GetLatestActiveAsync(user.Id, purpose);

        if (otp is null || otp.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException(InvalidMessageFor(purpose));
        }

        if (otp.AttemptCount >= _otpSettings.MaxVerifyAttempts)
        {
            throw new BusinessRuleException(AuthMessages.CodeBurned);
        }

        if (!_passwordHasher.Verify(otp.CodeHash, code))
        {
            otp.AttemptCount++;
            await _otpRepository.UpdateAsync(otp);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning(
                "OTP verification failed for user {UserId}, attempt {AttemptCount}", user.Id, otp.AttemptCount);

            throw new BusinessRuleException(InvalidMessageFor(purpose));
        }

        return otp;
    }

    private static string InvalidMessageFor(OtpPurpose purpose)
    {
        return purpose == OtpPurpose.ResetPassword
            ? AuthMessages.InvalidResetLink
            : AuthMessages.InvalidCode;
    }

    private static string GenerateNumericCode(int length)
    {
        var max = (int)Math.Pow(10, length);
        return RandomNumberGenerator.GetInt32(0, max).ToString().PadLeft(length, '0');
    }

    private static string GenerateResetToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
