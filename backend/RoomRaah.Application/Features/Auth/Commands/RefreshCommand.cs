using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Auth.Commands;

public class RefreshCommand : IRequest<AuthResponseDto>
{
    public string RefreshToken { get; set; } = string.Empty;
}
