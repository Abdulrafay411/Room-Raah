namespace RoomRaah.Application.DTOs;

/// <summary>
/// One recorded visit, as an admin sees it. There is no owner-facing shape derived
/// from this: what an owner is told is <see cref="OwnerInspectionStateDto"/>, and the
/// difference between the two is rule 115.
/// </summary>
public class PropertyInspectionDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing that was visited.</summary>
    public int PropertyId { get; set; }

    /// <summary>When the visit happened. Always in the past.</summary>
    public DateTime InspectedAt { get; set; }

    /// <summary>Passed or Failed.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>What the inspector wrote.</summary>
    public string? Notes { get; set; }

    /// <summary>What the owner was charged, if a fee was agreed.</summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>When that fee was collected, if it has been.</summary>
    public DateTime? FeeCollectedAt { get; set; }

    /// <summary>The admin who made the visit, by display name.</summary>
    public string InspectorDisplayName { get; set; } = string.Empty;
}

/// <summary>
/// What an owner is told about inspections on their own listing.
///
/// Rule 115 is the whole shape: whether their one request has been made, and the date
/// and verdict of visits that have <em>already happened</em>. There is no field here
/// for a scheduled visit, because no such date exists anywhere in the product — an
/// unannounced check that is announced a week ahead checks preparation rather than
/// reality.
/// </summary>
public class OwnerInspectionStateDto
{
    /// <summary>The listing.</summary>
    public int PropertyId { get; set; }

    /// <summary>Whether the owner has used their one request. Rule 108.</summary>
    public bool InspectionRequested { get; set; }

    /// <summary>When they asked.</summary>
    public DateTime? InspectionRequestedAt { get; set; }

    /// <summary>Whether the listing carries the badge today.</summary>
    public bool HasInspectionBadge { get; set; }

    /// <summary>When the last visit happened, if there has been one.</summary>
    public DateTime? LastInspectionAt { get; set; }
}

/// <summary>
/// A row on the admin's re-inspection worklist. Computed from the past — the last
/// visit and how long ago it was — rather than read from a schedule, because there is
/// no schedule to read.
/// </summary>
public class DueForInspectionDto
{
    /// <summary>The listing.</summary>
    public int PropertyId { get; set; }

    /// <summary>Its title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The area it is in.</summary>
    public string AreaName { get; set; } = string.Empty;

    /// <summary>The city that area is in.</summary>
    public string CityName { get; set; } = string.Empty;

    /// <summary>The owner, by display name. Rule 33 applies here as everywhere else.</summary>
    public string OwnerDisplayName { get; set; } = string.Empty;

    /// <summary>When it was last visited.</summary>
    public DateTime? LastInspectionAt { get; set; }

    /// <summary>How many days past the interval it is, so the list reads as a worklist.</summary>
    public int DaysOverdue { get; set; }
}

/// <summary>One entry on the badge's audit trail. Rule 112.</summary>
public class BadgeEventDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The listing.</summary>
    public int PropertyId { get; set; }

    /// <summary>Granted or Removed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Why. Required on both actions.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The admin who did it, by display name.</summary>
    public string ActedByDisplayName { get; set; } = string.Empty;

    /// <summary>When.</summary>
    public DateTime ActedAt { get; set; }
}
