using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class ApproveRevisionCommand : IRequest<RevisionDetailDto>, IRevisionDecision
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int RevisionId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>Optional on an approval.</summary>
    public string? AdminNote { get; set; }
}

public class RejectRevisionCommand : IRequest<RevisionDetailDto>, IRevisionDecision
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int RevisionId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>Required: rule 26.</summary>
    public string? AdminNote { get; set; }
}
