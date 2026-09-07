namespace RoomRaah.Application.DTOs;

/// <summary>
/// One account on the admin's user list.
///
/// Rule 117: **no CNIC and no phone number here.** A page of fifty accounts is fifty
/// identity numbers on one screen, in one screenshot and in one browser cache, for a
/// question the list is not being asked. The detail shape carries them; this does not,
/// and it is a separate type rather than a filtered version of one so that nothing can
/// widen it by accident.
/// </summary>
public class AdminUserSummaryDto
{
    /// <summary>Identifier.</summary>
    public int Id { get; set; }

    /// <summary>The name on the account.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>The address it signs in with.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Seeker, Owner or Admin.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Whether the email has been verified.</summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>Whether the account can sign in. Rule 11.</summary>
    public bool IsActive { get; set; }

    /// <summary>When it was created. The list is ordered by this, newest first.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// One account in full, and **the only shape in the product that carries a phone
/// number or a CNIC.**
///
/// This is the single exception rule 33 names, and it exists for one reason: an admin
/// recording the `OwnerIdentity` verification check is being asked to confirm an
/// identity, and rule 33 as originally written asked them to confirm it without being
/// shown it. The exception is deliberately narrow — this endpoint, Admin-only, and the
/// CNIC only for an owner because only an owner has one. Nothing changes about logging
/// or caching: §4.2 forbids a CNIC in a log line and the admin area is not cached.
/// </summary>
public class AdminUserDto : AdminUserSummaryDto
{
    /// <summary>
    /// The number the account signed up with. Rule 116, and nowhere else in the
    /// product.
    /// </summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>The owner's identity number, and null for every other role. Rule 116.</summary>
    public string? CnicNumber { get; set; }

    /// <summary>The owner's identity-check verdict, if this account is an owner.</summary>
    public string? IdentityStatus { get; set; }

    /// <summary>When that check was recorded.</summary>
    public DateTime? IdentityCheckedAt { get; set; }

    /// <summary>Whether this admin may record inspections, if this account is an admin.</summary>
    public bool? CanInspect { get; set; }
}
