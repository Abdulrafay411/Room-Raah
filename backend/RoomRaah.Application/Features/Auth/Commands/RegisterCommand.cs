using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Auth.Commands;

public class RegisterCommand : IRequest<UserSummaryDto>
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? CnicNumber { get; set; }
    public string? PayoutContact { get; set; }
}
