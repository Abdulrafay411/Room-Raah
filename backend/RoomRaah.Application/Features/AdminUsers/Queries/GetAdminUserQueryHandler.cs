using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminUsers.Queries;

public class GetAdminUserQueryHandler : IRequestHandler<GetAdminUserQuery, AdminUserDto>
{
    private readonly IUserRepository _users;
    private readonly IMapper _mapper;

    public GetAdminUserQueryHandler(IUserRepository users, IMapper mapper)
    {
        _users = users;
        _mapper = mapper;
    }

    public async Task<AdminUserDto> Handle(
        GetAdminUserQuery request, CancellationToken cancellationToken)
    {
        // GetByIdAsync ignores the query filters, which is what makes a deactivated
        // account visible here — an account that has been switched off is exactly the
        // one an admin is looking for.
        var user = await _users.GetByIdAsync(request.UserId)
            ?? throw new NotFoundException(InspectionMessages.UserNotFound(request.UserId));

        return _mapper.Map<AdminUserDto>(user);
    }
}
