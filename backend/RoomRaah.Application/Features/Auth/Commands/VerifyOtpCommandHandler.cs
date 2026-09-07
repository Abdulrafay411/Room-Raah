using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpRepository _otpRepository;
    private readonly IOtpService _otpService;
    private readonly IAuthSessionService _sessionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VerifyOtpCommandHandler> _logger;

    public VerifyOtpCommandHandler(
        IUserRepository userRepository,
        IOtpRepository otpRepository,
        IOtpService otpService,
        IAuthSessionService sessionService,
        IUnitOfWork unitOfWork,
        ILogger<VerifyOtpCommandHandler> logger)
    {
        _userRepository = userRepository;
        _otpRepository = otpRepository;
        _otpService = otpService;
        _sessionService = sessionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null)
        {
            throw new NotFoundException(AuthMessages.AccountNotFound);
        }

        if (!user.IsActive)
        {
            throw new BusinessRuleException(AuthMessages.AccountDeactivatedShort);
        }

        if (user.IsEmailVerified)
        {
            throw new BusinessRuleException(AuthMessages.EmailAlreadyVerified);
        }

        var otp = await _otpService.ValidateAsync(user, OtpPurpose.VerifyEmail, request.Code);

        otp.ConsumedAt = DateTime.UtcNow;
        await _otpRepository.UpdateAsync(otp);

        user.IsEmailVerified = true;
        await _userRepository.UpdateAsync(user);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User {UserId} verified their email address", user.Id);

        return await _sessionService.IssueAsync(user);
    }
}
