using RoomRaah.Domain.Enums;

namespace RoomRaah.Domain.Entities;

/// <summary>
/// One physical visit by a RoomRaah team member — the only paid thing in the product,
/// and the evidence behind the badge.
///
/// Note what is not here: a date the visit is *going* to happen. Rule 115 forbids a
/// future inspection date in any owner-facing response, and the way to be certain of
/// that is for no such date to exist anywhere. RoomRaah's schedule is not stored, and
/// the re-inspection queue is computed from the past instead.
/// </summary>
public class PropertyInspection
{
    public int Id { get; set; }
    public int PropertyId { get; set; }

    /// <summary>When the visit happened. Always in the past.</summary>
    public DateTime InspectedAt { get; set; }

    public int InspectorAdminId { get; set; }
    public InspectionResult Result { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// What the owner was charged for the visit. §Business Model collects it by bank
    /// transfer or wallet, so this is a record of an agreement rather than a payment
    /// the product processed — there is no gateway anywhere in this version.
    /// </summary>
    public decimal? FeeAmount { get; set; }

    public DateTime? FeeCollectedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Property Property { get; set; } = null!;
    public AppUser InspectorAdmin { get; set; } = null!;
}
