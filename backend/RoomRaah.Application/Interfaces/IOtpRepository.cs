using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IOtpRepository
{
    Task CreateAsync(OtpCode code);
    Task<OtpCode?> GetLatestActiveAsync(int appUserId, OtpPurpose purpose);
    Task InvalidateActiveAsync(int appUserId, OtpPurpose purpose);
    Task UpdateAsync(OtpCode code);
    /// <summary>
    /// Rule 126. Deletes every code that expired, or was consumed, before the cutoff,
    /// and answers with how many. Runs as one statement, so no unit of work follows it.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTime cutoff);
}
