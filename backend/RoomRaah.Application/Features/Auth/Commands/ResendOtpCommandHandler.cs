using MediatR;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;

    public ResendOtpCommandHandler(IUserRepository userRepository, IOtpService otpService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
    }

    public async Task Handle(ResendOtpCommand request, CancellationToken cancellationToken)
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

        await _otpService.IssueAsync(user, OtpPurpose.VerifyEmail);
    }
}
