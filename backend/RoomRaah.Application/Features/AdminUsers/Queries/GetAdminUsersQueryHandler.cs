using AutoMapper;
using MediatR;
using RoomRaah.Application.Common;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminUsers.Queries;

public class GetAdminUsersQueryHandler
    : IRequestHandler<GetAdminUsersQuery, PagedResult<AdminUserSummaryDto>>
{
    private readonly IUserRepository _users;
    private readonly IMapper _mapper;

    public GetAdminUsersQueryHandler(IUserRepository users, IMapper mapper)
    {
        _users = users;
        _mapper = mapper;
    }

    public async Task<PagedResult<AdminUserSummaryDto>> Handle(
        GetAdminUsersQuery request, CancellationToken cancellationToken)
    {
        var role = EnumName.Parse<UserRole>(request.Role);
        var search = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();

        var users = await _users.GetPageAsync(
            role, request.IsActive, search, request.Page, request.PageSize);

        return new PagedResult<AdminUserSummaryDto>
        {
            // Rule 117: the summary shape carries no CNIC and no phone number, and it
            // is a separate type rather than a filtered one so this list cannot gain
            // either by somebody widening the detail shape.
            Items = _mapper.Map<List<AdminUserSummaryDto>>(users),
            TotalCount = await _users.CountPageAsync(role, request.IsActive, search),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
