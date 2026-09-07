using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

public interface IVisitRequestRepository
{
    Task CreateAsync(VisitRequest request);

    /// <summary>
    /// One request with the listing, the listing's owner and the seeker attached.
    /// Every write path needs all three: the owner to check rule 37, the seeker to
    /// email, and the listing title to put in that email.
    /// </summary>
    Task<VisitRequest?> GetByIdAsync(int id);

    /// <summary>Rule 36: open means Requested or Accepted.</summary>
    Task<bool> HasOpenAsync(int seekerId, int propertyId);

    /// <summary>Rule 38: the gate a review has to pass.</summary>
    Task<bool> HasCompletedAsync(int seekerId, int propertyId);

    Task<List<VisitRequest>> GetForSeekerAsync(int seekerId, int page, int pageSize);
    Task<int> CountForSeekerAsync(int seekerId);

    Task<List<VisitRequest>> GetForOwnerAsync(int ownerId, int page, int pageSize);
    Task<int> CountForOwnerAsync(int ownerId);
}
