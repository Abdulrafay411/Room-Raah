using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminUsers.Commands;

/// <summary>
/// Rules 118 to 121. Two endpoints, one command: deactivating and reactivating differ
/// only in the value they write, and the three rules in front of both of them are the
/// same three rules.
///
/// §5 listed only the deactivation, which made an admin's misclick permanent and left
/// an account no route back even after whatever caused it was resolved.
/// </summary>
public class SetAccountActiveCommand : IRequest<AdminUserSummaryDto>
{
    /// <summary>Set from the route by the controller.</summary>
    public int UserId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int AdminId { get; set; }

    /// <summary>Set by the controller from which of the two endpoints was called.</summary>
    public bool Active { get; set; }

    /// <summary>Required. Rule 119.</summary>
    public string? Reason { get; set; }
}
