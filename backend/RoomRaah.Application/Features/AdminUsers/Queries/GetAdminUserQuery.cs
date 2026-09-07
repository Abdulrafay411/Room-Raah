using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminUsers.Queries;

/// <summary>
/// Rule 116, and the single exception to rule 33 in this product.
///
/// It exists because an admin recording the `OwnerIdentity` verification check is
/// being asked to confirm an identity, and rule 33 as originally written asked them to
/// confirm it without being shown it.
/// </summary>
public class GetAdminUserQuery : IRequest<AdminUserDto>
{
    public int UserId { get; set; }
}
