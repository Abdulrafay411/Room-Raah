using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpRepository _otpRepository;
    private readonly IOtpService _otpService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IOtpRepository otpRepository,
        IOtpService otpService,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _otpRepository = otpRepository;
        _otpService = otpService;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            throw new BusinessRuleException(AuthMessages.InvalidResetLink);
        }

        var otp = await _otpService.ValidateAsync(user, OtpPurpose.ResetPassword, request.Token);

        otp.ConsumedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _userRepository.UpdateAsync(user);

        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User {UserId} reset their password", user.Id);
    }
}
