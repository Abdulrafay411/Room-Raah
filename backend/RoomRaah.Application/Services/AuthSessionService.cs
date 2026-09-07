using AutoMapper;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Services;

public class AuthSessionService : IAuthSessionService
{
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly JwtSettings _jwtSettings;

    public AuthSessionService(
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IOptions<JwtSettings> jwtSettings)
    {
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponseDto> IssueAsync(AppUser user)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await _refreshTokenRepository.CreateAsync(new RefreshToken
        {
            AppUserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
        });

        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = _jwtSettings.AccessTokenMinutes * 60,
            User = _mapper.Map<UserSummaryDto>(user)
        };
    }
}
