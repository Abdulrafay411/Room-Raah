using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Commands;

public class ConfirmAvailabilityCommand : IRequest<OwnerListingUpdateDto>
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int OwnerId { get; set; }

    /// <summary>
    /// Nullable so that a body with no bed count is a missing field rather than a
    /// claim that no beds are free.
    /// </summary>
    public int? AvailableBeds { get; set; }
}
