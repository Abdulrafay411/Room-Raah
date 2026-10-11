namespace RoomRaah.Application.DTOs;

/// <summary>One verification check on a listing.</summary>
public class VerificationCheckDto
{
    /// <summary>OwnerIdentity, Address, Photos, Facilities or Availability.</summary>
    public string CheckType { get; set; } = string.Empty;

    /// <summary>NotChecked, Checked or Failed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The date of the evidence the admin looked at, if there was one.</summary>
    public DateTime? EvidenceDate { get; set; }

    /// <summary>What the admin wrote, if anything.</summary>
    public string? Note { get; set; }

    /// <summary>When the verdict was last recorded.</summary>
    public DateTime RecordedAt { get; set; }
}

/// <summary>
/// The owner of a listing, as an admin is allowed to see them. Rule 33 has no
/// exceptions and an admin panel is still a response, so there is no phone number
/// and no email address here.
/// </summary>
public class ListingOwnerSummaryDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>NotChecked, Checked or Failed.</summary>
    public string IdentityStatus { get; set; } = string.Empty;

    /// <summary>When the identity was checked, if it has been.</summary>
    public DateTime? IdentityCheckedAt { get; set; }
}

/// <summary>One row in the admin's listing queue.</summary>
public class AdminPropertySummaryDto
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

    /// <summary>Display name of the owner.</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Whether the listing carries the inspection badge. Rule 135.</summary>
    public bool HasInspectionBadge { get; set; }

    /// <summary>Monthly rent.</summary>
    public decimal MonthlyRent { get; set; }

    /// <summary>How many photos the listing holds.</summary>
    public int PhotoCount { get; set; }

    /// <summary>Thumbnail of the primary photo, or null while the listing has none.</summary>
    public string? PrimaryThumbnailUrl { get; set; }

    /// <summary>When the listing last changed — the queue is ordered by this, oldest first.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One listing as an admin reviews it.</summary>
public class AdminPropertyDto : OwnerPropertyDto
{
    /// <summary>The owner, without any way to contact them off the site.</summary>
    public ListingOwnerSummaryDto Owner { get; set; } = new();

    /// <summary>Every verification check recorded on the listing.</summary>
    public List<VerificationCheckDto> VerificationChecks { get; set; } = new();

    /// <summary>Whether rule 23 is satisfied and the listing may be published.</summary>
    public bool CanPublish { get; set; }
}

/// <summary>One field an owner proposes to change, rendered for a diff.</summary>
public class FieldChangeDto
{
    /// <summary>Name of the field.</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>What the live listing says today.</summary>
    public string? Current { get; set; }

    /// <summary>What the owner has asked for.</summary>
    public string? Proposed { get; set; }
}

/// <summary>One row in the content review queue.</summary>
public class RevisionQueueRowDto
{
    /// <summary>Identifier of the revision.</summary>
    public int Id { get; set; }

    /// <summary>Identifier of the listing it belongs to.</summary>
    public int PropertyId { get; set; }

    /// <summary>Title of that listing, as it stands today.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>Content or Availability.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Name of the area the listing is in.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>Name of the city that area is in.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>Display name of the owner.</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Names of the fields that differ from the live listing.</summary>
    public List<string> ChangedFields { get; set; } = new();

    /// <summary>Beds free today, so an availability row can be worked without opening it.</summary>
    public int CurrentAvailableBeds { get; set; }

    /// <summary>Beds the owner says are free.</summary>
    public int ProposedAvailableBeds { get; set; }

    /// <summary>Beds in the room in total.</summary>
    public int TotalBeds { get; set; }

    /// <summary>When the owner submitted it — the queue is ordered by this, oldest first.</summary>
    public DateTime SubmittedAt { get; set; }
}

/// <summary>A revision, the listing beside it, and the photos it moves.</summary>
public class RevisionDetailDto
{
    /// <summary>Identifier of the revision.</summary>
    public int Id { get; set; }

    /// <summary>Identifier of the listing it belongs to.</summary>
    public int PropertyId { get; set; }

    /// <summary>Title of that listing, as it stands today.</summary>
    public string PropertyTitle { get; set; } = string.Empty;

    /// <summary>Content or Availability.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Pending, Approved or Rejected.</summary>
    public string ReviewStatus { get; set; } = string.Empty;

    /// <summary>Display name of the owner.</summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// Every field that differs, rendered as text. A diff is read rather than
    /// computed against, and sixteen fields of four different types have no single
    /// typed shape that a client could use without a switch of its own.
    /// </summary>
    public List<FieldChangeDto> Changes { get; set; } = new();

    /// <summary>The gallery the public sees today.</summary>
    public List<PropertyPhotoDto> CurrentPhotos { get; set; } = new();

    /// <summary>Photos waiting to be added.</summary>
    public List<PropertyPhotoDto> PhotosToAdd { get; set; } = new();

    /// <summary>Photos waiting to be removed.</summary>
    public List<PropertyPhotoDto> PhotosToRemove { get; set; } = new();

    /// <summary>The proposed gallery order, first id first, or null if it is unchanged.</summary>
    public List<int>? ProposedPhotoOrder { get; set; }

    /// <summary>When the owner submitted it.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>When an admin decided, if one has.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>What the admin wrote, if anything.</summary>
    public string? AdminNote { get; set; }
}

/// <summary>What an owner gets back after editing a listing.</summary>
public class OwnerListingUpdateDto
{
    /// <summary>The listing as it stands — unchanged when the edit became a revision.</summary>
    public OwnerPropertyDto Listing { get; set; } = new();

    /// <summary>The revision the edit created or joined, or null when it was written straight in.</summary>
    public RevisionDetailDto? PendingRevision { get; set; }
}
