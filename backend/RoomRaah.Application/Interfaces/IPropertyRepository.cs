using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Interfaces;

/// <summary>
/// GetByIdAsync returns a tracked graph, so a handler that changes a listing or one
/// of its photos changes what the unit of work will save. There is no UpdateAsync:
/// a no-op method that looks like it saves is worse than no method at all.
/// </summary>
public interface IPropertyRepository
{
    Task CreateAsync(Property property);
    Task<Property?> GetByIdAsync(int id);
    Task<List<Property>> GetByOwnerAsync(int ownerId, int page, int pageSize);
    Task<int> CountByOwnerAsync(int ownerId);

    /// <summary>
    /// The admin's view of one listing: the same graph the owner sees plus the
    /// verification checks rule 23 asks about and the owner behind it. A separate
    /// method rather than a flag on GetByIdAsync, because the owner path must never
    /// be one boolean away from loading an account it has no business reading.
    /// </summary>
    Task<Property?> GetForReviewAsync(int id);

    /// <summary>
    /// The admin listing queue. <paramref name="search"/> is rule 135: title, city or
    /// owner's display name; null means no search.
    /// </summary>
    Task<List<Property>> GetByStatusAsync(PropertyStatus? status, string? search, int page, int pageSize);
    Task<int> CountByStatusAsync(PropertyStatus? status, string? search);

    /// <summary>
    /// One page of the public search. Rule 28 is applied here rather than by the
    /// caller: everything below only ever sees Published listings, so a new public
    /// read cannot forget it.
    /// </summary>
    Task<List<Property>> SearchAsync(PropertySearchCriteria criteria);

    Task<int> CountSearchAsync(PropertySearchCriteria criteria);

    /// <summary>One published listing in full, or null. Rule 28: null for every other status.</summary>
    Task<Property?> GetPublishedAsync(int id);

    /// <summary>
    /// One published listing with its owner and its area, and none of its collections.
    /// Rule 86 is asked by four handlers that need the listing to exist and one of
    /// them to email the owner; none of them needs the photos, and loading a gallery
    /// to file a report would be a hundred rows read to write one.
    /// </summary>
    Task<Property?> GetPublishedWithOwnerAsync(int id);

    /// <summary>The published listings among the given ids, for a comparison.</summary>
    Task<List<Property>> GetPublishedManyAsync(IReadOnlyCollection<int> ids);

    /// <summary>Rule 82: badge first, then the most recently confirmed availability.</summary>
    Task<List<Property>> GetFeaturedAsync(int count);

    /// <summary>
    /// Rule 125. Published listings whose availability was last confirmed before the
    /// cutoff, oldest first, with the owner attached because the sweep exists to email
    /// them. Capped, because a sweep that has to load every stale listing to run is a
    /// sweep that stops running on the day the backlog matters.
    /// </summary>
    Task<List<Property>> GetStaleAsync(DateTime confirmedBefore, int limit);

    /// <summary>
    /// Rule 114. Published listings that carry the badge and have not been visited
    /// since the cutoff, longest overdue first — computed from the last visit, because
    /// no future inspection date exists anywhere in the product to read instead.
    /// </summary>
    Task<List<Property>> GetDueForInspectionAsync(DateTime inspectedBefore, int page, int pageSize);

    Task<int> CountDueForInspectionAsync(DateTime inspectedBefore);

    /// <summary>
    /// Rule 136. Published listings whose owner asked for an inspection and that have
    /// not been inspected on or after the day of the request, oldest request first.
    /// </summary>
    Task<List<Property>> GetInspectionRequestedAsync(int page, int pageSize);

    Task<int> CountInspectionRequestedAsync();
}
