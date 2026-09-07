using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminUsers.Commands;

public class SetAccountActiveCommandHandler
    : IRequestHandler<SetAccountActiveCommand, AdminUserSummaryDto>
{
    private readonly IUserRepository _users;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<SetAccountActiveCommandHandler> _logger;

    public SetAccountActiveCommandHandler(
        IUserRepository users,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<SetAccountActiveCommandHandler> logger)
    {
        _users = users;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<AdminUserSummaryDto> Handle(
        SetAccountActiveCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId)
            ?? throw new NotFoundException(InspectionMessages.UserNotFound(request.UserId));

        // Rule 119, before the state checks: an absent field is a rule about the shape
        // of the request and is answered first.
        var reason = AccountState.RequireReason(request.Reason);

        // Rule 118.
        AccountState.RequireChangeable(user, request.AdminId);

        // Rule 121.
        AccountState.RequireNotAlready(user, request.Active);

        user.IsActive = request.Active;

        await _unitOfWork.SaveChangesAsync();

        // Rule 120. Nothing the account owns is modified — the query filters already
        // take a deactivated owner's listings out of every public list, and writing a
        // status onto each one as well would be a second source of truth for the same
        // fact, left behind after a reactivation that was meant to undo everything.
        //
        // The cache is a different matter: a listing already sitting in a cached search
        // page does not leave it because a filter changed.
        if (user.Role == UserRole.Owner)
        {
            await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
        }

        // Rule 119. Warning, not Information: this is one of the refusals-and-actions
        // worth finding without reading every line, the same as a failed sign-in. The
        // reason is written here because §4 names no table that stores one.
        _logger.LogWarning(
            "Admin {AdminId} {Action} account {UserId}: {Reason}",
            request.AdminId, request.Active ? "reactivated" : "deactivated", user.Id, reason);

        return _mapper.Map<AdminUserSummaryDto>(user);
    }
}
