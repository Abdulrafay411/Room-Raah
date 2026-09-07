using System.Text.Json.Serialization;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Listings;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UpdatePropertyCommand : IRequest<OwnerListingUpdateDto>, IListingFields
{
    /// <summary>Set from the route by the controller.</summary>
    [JsonIgnore]
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller. Anything sent in the body is ignored.</summary>
    [JsonIgnore]
    public int OwnerId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>Nullable for the same reason as on the create command: 0.0 is a real coordinate.</summary>
    public double? Latitude { get; set; }

    /// <summary>Nullable for the same reason as <see cref="Latitude"/>.</summary>
    public double? Longitude { get; set; }

    public string RoomType { get; set; } = string.Empty;
    public string GenderPolicy { get; set; } = string.Empty;
    public int? TotalBeds { get; set; }
    public int? AvailableBeds { get; set; }
    public decimal? MonthlyRent { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public decimal? UtilitiesCharge { get; set; }
    public decimal? MessCharge { get; set; }
    public string? HouseRules { get; set; }
    public int AreaId { get; set; }
    public List<int> FacilityIds { get; set; } = new();
}
