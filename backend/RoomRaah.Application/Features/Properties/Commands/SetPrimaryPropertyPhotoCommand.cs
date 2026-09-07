using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Commands;

public class SetPrimaryPropertyPhotoCommand : IRequest<OwnerListingUpdateDto>
{
    public int PropertyId { get; set; }

    public int PhotoId { get; set; }

    /// <summary>Set from the bearer token by the controller.</summary>
    public int OwnerId { get; set; }
}
