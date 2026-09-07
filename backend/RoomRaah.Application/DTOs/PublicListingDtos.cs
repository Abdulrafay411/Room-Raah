namespace RoomRaah.Application.DTOs;

/// <summary>
/// A listing as a stranger sees it: on a search card, on the landing page or in a
/// comparison. Deliberately not derived from <c>OwnerPropertyDto</c> — the owner and
/// admin shapes gain fields as those panels grow, and a public page must never
/// inherit one of them by accident.
/// </summary>
public class ListingCardDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>Listing title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Identifier of the area the listing is in.</summary>
    public int AreaId { get; set; }

    /// <summary>Name of that area.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>Identifier of the city that area is in.</summary>
    public int CityId { get; set; }

    /// <summary>Name of that city.</summary>
    public string CityName { get; set; } = string.Empty;

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

    /// <summary>Latitude of the pin, so the map view can render from the same response.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude of the pin.</summary>
    public double Longitude { get; set; }

    /// <summary>Thumbnail of the primary photo.</summary>
    public string? PrimaryThumbnailUrl { get; set; }

    /// <summary>How many photos the public gallery holds.</summary>
    public int PhotoCount { get; set; }

    /// <summary>Whether the listing carries the physical inspection badge.</summary>
    public bool HasInspectionBadge { get; set; }

    /// <summary>When the owner last confirmed availability.</summary>
    public DateTime AvailabilityConfirmedAt { get; set; }

    /// <summary>
    /// Straight-line kilometres from the landmark the search named, to one decimal.
    /// Null when no landmark was named — an absent distance, not a distance of zero.
    /// </summary>
    public double? DistanceKm { get; set; }
}

/// <summary>One photo in a public gallery. Carries no status: rule 79.</summary>
public class ListingPhotoDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The full-size image.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The generated thumbnail.</summary>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>Whether this is the photo shown on the listing card.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Position in the gallery, starting at 1.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Everything a public response is allowed to say about an owner. Rule 84: a display
/// name, whether their identity was checked, and when.
/// </summary>
public class ListingOwnerDto
{
    /// <summary>The name the owner signed up with.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Whether an admin has checked who they are.</summary>
    public bool IdentityVerified { get; set; }

    /// <summary>The date of that check, when there is one.</summary>
    public DateTime? IdentityCheckedAt { get; set; }
}

/// <summary>
/// One trust check as a visitor sees it. The admin's note and the admin's id stay on
/// the admin side of the product: rule 84.
/// </summary>
public class ListingCheckDto
{
    /// <summary>OwnerIdentity, Address, Photos, Facilities or Availability.</summary>
    public string CheckType { get; set; } = string.Empty;

    /// <summary>NotChecked, Checked or Failed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The date the evidence is dated, when the check has been made.</summary>
    public DateTime? EvidenceDate { get; set; }
}

/// <summary>One listing in a side-by-side comparison: the card plus what is compared.</summary>
public class ListingComparisonDto : ListingCardDto
{
    /// <summary>Street address.</summary>
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>The facilities the listing offers.</summary>
    public List<FacilityDto> Facilities { get; set; } = new();
}

/// <summary>A published listing in full, on its public detail page.</summary>
public class ListingDetailDto : ListingCardDto
{
    /// <summary>Full description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Street address. Public: it is listing information, per section 2.</summary>
    public string AddressLine { get; set; } = string.Empty;

    /// <summary>House rules, if the owner set any.</summary>
    public string? HouseRules { get; set; }

    /// <summary>The facilities the listing offers.</summary>
    public List<FacilityDto> Facilities { get; set; } = new();

    /// <summary>The public gallery, in order.</summary>
    public List<ListingPhotoDto> Photos { get; set; } = new();

    /// <summary>The owner, as much of them as a public response may name.</summary>
    public ListingOwnerDto Owner { get; set; } = new();

    /// <summary>The trust checks recorded against this listing.</summary>
    public List<ListingCheckDto> Checks { get; set; } = new();

    /// <summary>When the listing was created.</summary>
    public DateTime CreatedAt { get; set; }
}
