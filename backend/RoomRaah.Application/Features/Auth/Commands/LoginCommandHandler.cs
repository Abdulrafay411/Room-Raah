using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthSessionService _sessionService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuthSessionService sessionService,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email);

        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            _logger.LogWarning("Login failed for {Email}", email);
            throw new UnauthenticatedException(AuthMessages.InvalidCredentials);
        }

        // Rule 137. After the password, never before it: a wrong password stays the
        // ordinary 401, so the reason cannot be read by someone guessing addresses.
        if (!user.IsActive)
        {
            throw new BusinessRuleException(AuthMessages.AccountDeactivatedBecause(user.DeactivationReason));
        }

        var response = await _sessionService.IssueAsync(user);

        _logger.LogInformation("User {UserId} logged in", user.Id);

        return response;
    }
}
