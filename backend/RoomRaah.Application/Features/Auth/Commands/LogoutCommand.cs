using System.Text.Json.Serialization;
using MediatR;

namespace RoomRaah.Application.Features.Auth.Commands;

public class LogoutCommand : IRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AppUserId { get; set; }
}
