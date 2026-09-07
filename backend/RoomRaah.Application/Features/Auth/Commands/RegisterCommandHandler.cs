using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, UserSummaryDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOtpService otpService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserSummaryDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.EmailExistsAsync(email))
        {
            throw new BusinessRuleException(AuthMessages.EmailAlreadyTaken);
        }

        var role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = role,
            IsEmailVerified = false,
            IsActive = true
        };

        if (role == UserRole.Owner)
        {
            user.OwnerProfile = new OwnerProfile
            {
                CnicNumber = request.CnicNumber!.Trim(),
                IdentityStatus = VerificationStatus.NotChecked,
                PayoutContact = request.PayoutContact?.Trim() ?? string.Empty
            };
        }
        else
        {
            user.SeekerProfile = new SeekerProfile();
        }

        await _userRepository.CreateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("User {UserId} registered as {Role}", user.Id, user.Role);

        await _otpService.IssueAsync(user, OtpPurpose.VerifyEmail);

        return _mapper.Map<UserSummaryDto>(user);
    }
}
