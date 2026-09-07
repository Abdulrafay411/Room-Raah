using MediatR;

namespace RoomRaah.Application.Features.Properties.Commands;

public class DeletePropertyPhotoCommand : IRequest
{
    public int PropertyId { get; set; }

    public int PhotoId { get; set; }

    /// <summary>Set from the bearer token by the controller.</summary>
    public int OwnerId { get; set; }
}
