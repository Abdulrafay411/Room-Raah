using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// Rule 41 is enforced in the three public reads below, not in the handler that
/// calls them: a review that has not been approved is invisible, and the average
/// and the count are taken over the same set the page is taken from.
/// </summary>
public interface IReviewRepository
{
    Task CreateAsync(Review review);

    /// <summary>Rule 39: one review per author per listing, whatever its moderation state.</summary>
    Task<bool> ExistsForAsync(int authorId, int propertyId);

    Task<Review?> GetByIdAsync(int id);

    Task<List<Review>> GetApprovedAsync(int propertyId, int page, int pageSize);
    Task<int> CountApprovedAsync(int propertyId);

    /// <summary>The mean of the approved ratings, or null when there are none.</summary>
    Task<double?> AverageApprovedAsync(int propertyId);

    Task<List<Review>> GetQueueAsync(ModerationState? state, int page, int pageSize);
    Task<int> CountQueueAsync(ModerationState? state);
}
