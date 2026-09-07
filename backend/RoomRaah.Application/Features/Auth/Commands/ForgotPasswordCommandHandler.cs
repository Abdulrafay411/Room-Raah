using MediatR;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Auth.Commands;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpService _otpService;

    public ForgotPasswordCommandHandler(IUserRepository userRepository, IOtpService otpService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            return;
        }

        await _otpService.IssueAsync(user, OtpPurpose.ResetPassword);
    }
}
