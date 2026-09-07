using MediatR;

namespace RoomRaah.Application.Features.Auth.Commands;

public class ForgotPasswordCommand : IRequest
{
    public string Email { get; set; } = string.Empty;
}
