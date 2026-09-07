using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Commands;

public class SubmitPropertyCommand : IRequest<OwnerPropertyDto>
{
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller.</summary>
    public int OwnerId { get; set; }
}
