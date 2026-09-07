namespace RoomRaah.Application.DTOs;

/// <summary>One of the signed-in owner's listings, in full.</summary>
public class OwnerPropertyDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>Listing title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Street address, shown only to the owner and to admins.</summary>
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>Latitude of the pin the owner dropped.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the pin the owner dropped.</summary>
    public double Longitude { get; set; }

    /// <summary>Private or Shared.</summary>
    public string RoomType { get; set; } = string.Empty;

    /// <summary>Boys, Girls or Family.</summary>
    public string GenderPolicy { get; set; } = string.Empty;

    /// <summary>Beds in the room in total.</summary>
    public int TotalBeds { get; set; }

    /// <summary>Beds currently free.</summary>
    public int AvailableBeds { get; set; }

    /// <summary>Monthly rent.</summary>
    public decimal MonthlyRent { get; set; }

    /// <summary>Refundable security deposit.</summary>
    public decimal SecurityDeposit { get; set; }

    /// <summary>Monthly utilities charge.</summary>
    public decimal UtilitiesCharge { get; set; }

    /// <summary>Monthly mess charge.</summary>
    public decimal MessCharge { get; set; }

    /// <summary>House rules, if the owner set any.</summary>
    public string? HouseRules { get; set; }

    /// <summary>Draft, PendingReview, ChangesRequested, Published, Rejected or Unpublished.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Whether the listing carries the physical inspection badge.</summary>
    public bool HasInspectionBadge { get; set; }

    /// <summary>When availability was last confirmed.</summary>
    public DateTime AvailabilityConfirmedAt { get; set; }

    /// <summary>Identifier of the area the listing is in.</summary>
    public int AreaId { get; set; }

    /// <summary>Name of that area.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>Identifier of the city that area is in.</summary>
    public int CityId { get; set; }

    /// <summary>Name of that city.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>The facilities the listing offers.</summary>
    public List<FacilityDto> Facilities { get; set; } = new();

    /// <summary>Every photo on the listing, in gallery order.</summary>
    public List<PropertyPhotoDto> Photos { get; set; } = new();

    /// <summary>When the listing was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the listing was last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}
