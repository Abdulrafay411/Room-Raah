using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// GetByIdAsync and GetPendingAsync return tracked graphs, because every caller of
/// either is about to change the revision or the listing under it. The queue reads
/// do not track: nothing writes through a queue page.
/// </summary>
public interface IPropertyRevisionRepository
{
    Task CreateAsync(PropertyRevision revision);
    Task<PropertyRevision?> GetByIdAsync(int id);
    Task<PropertyRevision?> GetPendingAsync(int propertyId, RevisionKind kind);
    Task<List<PropertyRevision>> GetQueueAsync(RevisionKind? kind, int page, int pageSize);
    Task<int> CountQueueAsync(RevisionKind? kind);
}
