using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class RefreshCommandHandler : IRequestHandler<RefreshCommand, AuthResponseDto>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IAuthSessionService _sessionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RefreshCommandHandler> _logger;

    public RefreshCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        ITokenService tokenService,
        IAuthSessionService sessionService,
        IUnitOfWork unitOfWork,
        ILogger<RefreshCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository = userRepository;
        _tokenService = tokenService;
        _sessionService = sessionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AuthResponseDto> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        if (await _tokenService.IsRefreshTokenRevoked(request.RefreshToken))
        {
            throw new UnauthenticatedException(AuthMessages.InvalidSession);
        }

        var stored = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthenticatedException(AuthMessages.InvalidSession);
        }

        var user = await _userRepository.GetByIdAsync(stored.AppUserId);

        if (user is null || !user.IsActive)
        {
            throw new UnauthenticatedException(AuthMessages.InvalidSession);
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        var response = await _sessionService.IssueAsync(user);

        _logger.LogInformation("User {UserId} refreshed token", user.Id);

        return response;
    }
}
