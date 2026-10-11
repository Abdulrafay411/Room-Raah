using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class PropertyRepository : IPropertyRepository
{
    private readonly RoomRaahDbContext _context;
    private readonly IDistanceCalculator _distance;

    public PropertyRepository(RoomRaahDbContext context, IDistanceCalculator distance)
    {
        _context = context;
        _distance = distance;
    }

    public Task CreateAsync(Property property)
    {
        _context.Properties.Add(property);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tracked on purpose: every write path in the owner area loads a listing
    /// through here and then changes it or one of its photos.
    ///
    /// Split for the reason <see cref="GetPublishedAsync"/> is split — two collections
    /// in one query multiply by each other, so four photos and five facilities is one
    /// listing arriving as twenty rows. EF Core warns about exactly this and the warning
    /// was in the log until it did not have to be. Splitting does not affect tracking:
    /// what it gives up is one transaction across the sub-queries, and the only writer of
    /// an owner's own listing is that owner, in this request.
    /// </summary>
    public async Task<Property?> GetByIdAsync(int id)
    {
        return await _context.Properties
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Include(p => p.Facilities).ThenInclude(f => f.Facility)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Property>> GetByOwnerAsync(int ownerId, int page, int pageSize)
    {
        return await _context.Properties
            .AsNoTracking()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByOwnerAsync(int ownerId)
    {
        return await _context.Properties.CountAsync(p => p.OwnerId == ownerId);
    }

    /// <summary>
    /// Three collections, so split for the same reason as the two above: four photos,
    /// five facilities and five checks is one listing arriving as a hundred rows, on the
    /// query an admin runs before every single decision.
    /// </summary>
    public async Task<Property?> GetForReviewAsync(int id)
    {
        return await _context.Properties
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Include(p => p.Facilities).ThenInclude(f => f.Facility)
            .Include(p => p.VerificationChecks)
            .Include(p => p.Owner).ThenInclude(o => o.OwnerProfile)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Property>> GetByStatusAsync(PropertyStatus? status, int page, int pageSize)
    {
        return await InStatus(status)
            .AsNoTracking()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Include(p => p.Owner)
            .OrderBy(p => p.UpdatedAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByStatusAsync(PropertyStatus? status)
    {
        return await InStatus(status).CountAsync();
    }

    private IQueryable<Property> InStatus(PropertyStatus? status)
    {
        return status is null
            ? _context.Properties
            : _context.Properties.Where(p => p.Status == status);
    }

    public async Task<List<Property>> SearchAsync(PropertySearchCriteria criteria)
    {
        return await ListingOrder.Apply(Matching(criteria), criteria, _distance)
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountSearchAsync(PropertySearchCriteria criteria)
    {
        return await Matching(criteria).CountAsync();
    }

    /// <summary>
    /// Split, because this loads three collections at once and a single query would
    /// multiply them by each other: four photos, five facilities and five checks is
    /// one listing arriving as a hundred rows. EF Core warns about exactly this, and
    /// the detail page is the most-read query in the product. It is safe here for the
    /// reason split queries usually are not — the result is a snapshot that is about
    /// to be cached, not a read that something is about to be written against.
    /// </summary>
    public async Task<Property?> GetPublishedAsync(int id)
    {
        return await Published()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Include(p => p.Facilities).ThenInclude(f => f.Facility)
            .Include(p => p.VerificationChecks)
            .Include(p => p.Owner)
            .AsSplitQuery()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Rule 86's read. No collections at all: the four handlers that ask this need
    /// the listing to exist and one of them needs the owner to email, and loading a
    /// gallery in order to file a report would be a hundred rows read to write one.
    /// </summary>
    public async Task<Property?> GetPublishedWithOwnerAsync(int id)
    {
        return await Published()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Property>> GetPublishedManyAsync(IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var wanted = ids.ToList();

        // Ordered because a split query needs one: without it the pages of a
        // second query are not guaranteed to line up with the first. The handler
        // puts these back into the order the ids were given.
        return await Published()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .Include(p => p.Facilities).ThenInclude(f => f.Facility)
            .Where(p => wanted.Contains(p.Id))
            .OrderBy(p => p.Id)
            .AsSplitQuery()
            .ToListAsync();
    }

    /// <summary>
    /// Rule 125. Oldest confirmation first, so a capped run works through the backlog
    /// rather than circling the same rows, and broken by the id for the reason rule 77
    /// gives — listings confirmed in the same second are otherwise free to swap places
    /// between one run and the next, which for a capped sweep means some are never seen.
    /// </summary>
    public Task<List<Property>> GetStaleAsync(DateTime confirmedBefore, int limit)
    {
        return _context.Properties
            .Include(p => p.Owner)
            .Where(p => p.Status == PropertyStatus.Published &&
                        p.AvailabilityConfirmedAt < confirmedBefore)
            .OrderBy(p => p.AvailabilityConfirmedAt)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Rule 114. Badged, published, and not visited since the cutoff — longest overdue
    /// first, so the worklist reads as a worklist. A badged listing that has somehow
    /// never been inspected sorts to the very top, which is where it belongs.
    /// </summary>
    public Task<List<Property>> GetDueForInspectionAsync(
        DateTime inspectedBefore, int page, int pageSize)
    {
        return DueForInspection(inspectedBefore)
            .AsNoTracking()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Owner)
            .OrderBy(p => p.LastInspectionAt)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountDueForInspectionAsync(DateTime inspectedBefore)
    {
        return DueForInspection(inspectedBefore).CountAsync();
    }

    private IQueryable<Property> DueForInspection(DateTime inspectedBefore)
    {
        return _context.Properties.Where(p =>
            p.Status == PropertyStatus.Published &&
            p.HasInspectionBadge &&
            (p.LastInspectionAt == null || p.LastInspectionAt < inspectedBefore));
    }

    public async Task<List<Property>> GetFeaturedAsync(int count)
    {
        return await Published()
            .Include(p => p.Area).ThenInclude(a => a.City)
            .Include(p => p.Photos)
            .OrderByDescending(p => p.HasInspectionBadge)
            .ThenByDescending(p => p.AvailabilityConfirmedAt)
            .ThenBy(p => p.Id)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Rule 28, in one place. Every public read below starts here, so a query added
    /// later cannot forget the one condition that separates a listing a stranger may
    /// see from a draft nobody may. IsActive, and the state of the owner, the area
    /// and the city, come from the global query filter.
    /// </summary>
    private IQueryable<Property> Published()
    {
        return _context.Properties
            .AsNoTracking()
            .Where(p => p.Status == PropertyStatus.Published);
    }

    private IQueryable<Property> Matching(PropertySearchCriteria criteria)
    {
        var query = Published();

        if (criteria.CityId is { } cityId)
        {
            query = query.Where(p => p.Area.CityId == cityId);
        }

        if (criteria.AreaId is { } areaId)
        {
            query = query.Where(p => p.AreaId == areaId);
        }

        if (criteria.MinRent is { } minRent)
        {
            query = query.Where(p => p.MonthlyRent >= minRent);
        }

        if (criteria.MaxRent is { } maxRent)
        {
            query = query.Where(p => p.MonthlyRent <= maxRent);
        }

        if (criteria.RoomType is { } roomType)
        {
            query = query.Where(p => p.RoomType == roomType);
        }

        if (criteria.GenderPolicy is { } genderPolicy)
        {
            query = query.Where(p => p.GenderPolicy == genderPolicy);
        }

        // Rule 78.
        if (criteria.AvailableOnly)
        {
            query = query.Where(p => p.AvailableBeds > 0);
        }

        // Rule 76: one EXISTS per facility rather than one over a set. Somebody who
        // ticks Wi-Fi and laundry needs both, and a single IN clause hands them either.
        foreach (var facilityId in criteria.FacilityIds.Distinct())
        {
            var id = facilityId;
            query = query.Where(p => p.Facilities.Any(f => f.FacilityId == id));
        }

        if (criteria is
            { MaxDistanceKm: { } kilometres, LandmarkLatitude: { } lat, LandmarkLongitude: { } lng })
        {
            query = query.Where(_distance.Within(lat, lng, kilometres));
        }

        // Rule 134: the part of the map the person is looking at, edges included. The
        // validator has already made sure the four edges arrive together.
        if (criteria is
            {
                MinLatitude: { } south, MaxLatitude: { } north,
                MinLongitude: { } west, MaxLongitude: { } east
            })
        {
            query = query.Where(p =>
                p.Latitude >= south && p.Latitude <= north &&
                p.Longitude >= west && p.Longitude <= east);
        }

        return query;
    }

}
