using MediatR;

namespace RoomRaah.Application.Features.Saved.Commands;

public class UnsavePropertyCommand : IRequest
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int UserId { get; set; }

    public int PropertyId { get; set; }
}
