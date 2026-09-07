using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Auth.Commands;

public class VerifyOtpCommand : IRequest<AuthResponseDto>
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
