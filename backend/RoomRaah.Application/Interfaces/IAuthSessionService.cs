using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IAuthSessionService
{
    Task<AuthResponseDto> IssueAsync(AppUser user);
}
