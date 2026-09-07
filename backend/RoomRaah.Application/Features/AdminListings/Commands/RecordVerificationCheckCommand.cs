using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RecordVerificationCheckCommand : IRequest<List<VerificationCheckDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int AdminId { get; set; }

    /// <summary>OwnerIdentity, Address, Photos, Facilities or Availability.</summary>
    public string CheckType { get; set; } = string.Empty;

    /// <summary>NotChecked, Checked or Failed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The date of the evidence, where the check was made against something dated.</summary>
    public DateTime? EvidenceDate { get; set; }

    /// <summary>What the admin wants on the record.</summary>
    public string? Note { get; set; }
}
