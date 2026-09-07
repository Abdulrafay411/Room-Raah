using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Commands;

public class ReorderPropertyPhotosCommand : IRequest<OwnerListingUpdateDto>
{
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int OwnerId { get; set; }

    /// <summary>Every photo on the listing, in the order it should appear.</summary>
    public List<int> PhotoIds { get; set; } = new();
}
