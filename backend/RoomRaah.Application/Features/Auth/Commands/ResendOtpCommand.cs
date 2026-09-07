using MediatR;

namespace RoomRaah.Application.Features.Auth.Commands;

public class ResendOtpCommand : IRequest
{
    public string Email { get; set; } = string.Empty;
}
