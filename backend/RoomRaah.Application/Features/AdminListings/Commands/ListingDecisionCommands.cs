using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class ApproveListingCommand : IRequest<AdminPropertyDto>, IListingDecision
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>Optional on an approval — there is nothing for the owner to act on.</summary>
    public string? AdminNote { get; set; }
}

public class RejectListingCommand : IRequest<AdminPropertyDto>, IListingDecision
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>Required: rule 26.</summary>
    public string? AdminNote { get; set; }
}

public class RequestListingChangesCommand : IRequest<AdminPropertyDto>, IListingDecision
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>Required: rule 63. It is the entire content of the message to the owner.</summary>
    public string? AdminNote { get; set; }
}
