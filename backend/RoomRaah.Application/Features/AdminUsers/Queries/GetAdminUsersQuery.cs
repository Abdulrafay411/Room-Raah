using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminUsers.Queries;

/// <summary>
/// Rule 117. The whole `Admin — users` area is in §5 and in the twenty-page structure
/// and belonged to no phase of the build plan at all — three endpoints that following
/// the plan would have missed entirely.
/// </summary>
public class GetAdminUsersQuery : IRequest<PagedResult<AdminUserSummaryDto>>
{
    /// <summary>Optional. Seeker, Owner or Admin.</summary>
    public string? Role { get; set; }

    /// <summary>
    /// Optional. Left unset the list shows deactivated accounts too, which is the
    /// point of an admin user list — an account that has been switched off is exactly
    /// the one somebody is looking for.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>Optional. Matches a name or an email address.</summary>
    public string? Q { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
