using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Auth.Commands;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<LogoutCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (stored is null || stored.AppUserId != request.AppUserId)
        {
            throw new NotFoundException(AuthMessages.RefreshTokenNotFound);
        }

        var remaining = stored.ExpiresAt - DateTime.UtcNow;

        if (remaining > TimeSpan.Zero)
        {
            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, remaining);
        }

        if (stored.RevokedAt is null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("User {UserId} logged out", request.AppUserId);
    }
}
