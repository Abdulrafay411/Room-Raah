using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IOtpService
{
    Task IssueAsync(AppUser user, OtpPurpose purpose);
    Task<OtpCode> ValidateAsync(AppUser user, OtpPurpose purpose, string code);
}
