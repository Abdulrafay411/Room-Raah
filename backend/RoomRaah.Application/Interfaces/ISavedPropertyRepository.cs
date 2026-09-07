using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// A shortlist. Rule 87 lives in the two reads below rather than in the handler:
/// the page and the count both hide a listing that has left the public site, and
/// they have to agree or the last page of a shortlist is empty.
/// </summary>
public interface ISavedPropertyRepository
{
    /// <summary>The row for this pair, or null. Rule 34 is asked through this.</summary>
    Task<SavedProperty?> GetAsync(int userId, int propertyId);

    Task CreateAsync(SavedProperty saved);

    /// <summary>Removes the row. Works even when the listing is no longer published: rule 87.</summary>
    void Remove(SavedProperty saved);

    /// <summary>One page of the shortlist, newest save first, published listings only.</summary>
    Task<List<SavedProperty>> GetPageAsync(int userId, int page, int pageSize);

    Task<int> CountAsync(int userId);
}
