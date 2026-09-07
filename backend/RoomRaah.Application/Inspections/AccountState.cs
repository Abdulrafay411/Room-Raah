using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Inspections;

/// <summary>
/// Rules 118 and 121: who an admin may switch off, and what happens when they try to
/// switch off something that is already off.
///
/// Small enough to inline and kept out of the handler anyway, because these are the
/// two ways an admin panel locks everybody out of itself and neither is obvious from
/// inside the handler that does the switching.
/// </summary>
public static class AccountState
{
    /// <summary>
    /// Rule 118. Two separate protections that look like one: an admin may not switch
    /// off their own account, and no admin account may be switched off from here at
    /// all. Without the second, two admins can lock each other out; without the first,
    /// one admin can lock themselves out on their own.
    /// </summary>
    public static void RequireChangeable(AppUser target, int actingAdminId)
    {
        if (target.Id == actingAdminId)
        {
            throw new BusinessRuleException(InspectionMessages.CannotDeactivateSelf);
        }

        if (target.Role == UserRole.Admin)
        {
            throw new UnauthorizedAccessException(InspectionMessages.CannotChangeAnotherAdmin);
        }
    }

    /// <summary>
    /// Rule 121. Refused rather than treated as already done, because unlike marking a
    /// conversation read this is not something a client does twice by accident — a
    /// second deactivation means somebody is acting on a stale view of the account.
    /// </summary>
    public static void RequireNotAlready(AppUser target, bool active)
    {
        if (target.IsActive == active)
        {
            throw new BusinessRuleException(active
                ? InspectionMessages.AlreadyActive
                : InspectionMessages.AlreadyDeactivated);
        }
    }

    /// <summary>Rule 119. A reason on both, for the same reason a badge change needs one.</summary>
    public static string RequireReason(string? reason) => BadgeRules.RequireReason(reason);
}
