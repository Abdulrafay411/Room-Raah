using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

public interface IReportRepository
{
    Task CreateAsync(Report report);

    /// <summary>Rule 42 as amended: unresolved means Open or UnderReview.</summary>
    Task<bool> HasUnresolvedAsync(int userId, int propertyId);

    Task<Report?> GetByIdAsync(int id);

    Task<List<Report>> GetQueueAsync(ReportStatus? status, int page, int pageSize);
    Task<int> CountQueueAsync(ReportStatus? status);
}
