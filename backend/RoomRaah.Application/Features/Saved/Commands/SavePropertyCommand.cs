using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Saved.Commands;

public class SavePropertyCommand : IRequest<SavedListingDto>
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int UserId { get; set; }

    public int PropertyId { get; set; }
}
