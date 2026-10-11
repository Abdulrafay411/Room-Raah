namespace RoomRaah.Application.DTOs;

/// <summary>One row on the owner's dashboard.</summary>
public class OwnerPropertySummaryDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>Listing title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Draft, PendingReview, ChangesRequested, Published, Rejected or Unpublished.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Name of the area the listing is in.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>Name of the city that area is in.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>Monthly rent.</summary>
    public decimal MonthlyRent { get; set; }

    /// <summary>Beds currently free.</summary>
    public int AvailableBeds { get; set; }

    /// <summary>Beds in the room in total.</summary>
    public int TotalBeds { get; set; }

    /// <summary>How many photos the listing holds.</summary>
    public int PhotoCount { get; set; }

    /// <summary>Thumbnail of the primary photo, or null while the listing has none.</summary>
    public string? PrimaryThumbnailUrl { get; set; }

    /// <summary>Whether the listing carries the physical inspection badge.</summary>
    public bool HasInspectionBadge { get; set; }

    /// <summary>
    /// Rule 138: when the badge was taken off, if that is the latest thing to have
    /// happened to it. Null when it was never removed or has been granted again.
    /// </summary>
    public DateTime? BadgeRemovedAt { get; set; }

    /// <summary>Rule 138: the reason the admin gave. The admin is not named.</summary>
    public string? BadgeRemovedReason { get; set; }

    /// <summary>When availability was last confirmed.</summary>
    public DateTime AvailabilityConfirmedAt { get; set; }

    /// <summary>When the listing was last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}
