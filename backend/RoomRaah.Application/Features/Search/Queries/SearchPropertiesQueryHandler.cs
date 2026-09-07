using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Common;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Search.Queries;

public class SearchPropertiesQueryHandler
    : IRequestHandler<SearchPropertiesQuery, PagedResult<ListingCardDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly ILocationRepository _locations;
    private readonly IFacilityRepository _facilities;
    private readonly IDistanceCalculator _distance;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public SearchPropertiesQueryHandler(
        IPropertyRepository properties,
        ILocationRepository locations,
        IFacilityRepository facilities,
        IDistanceCalculator distance,
        ICacheService cache,
        IMapper mapper)
    {
        _properties = properties;
        _locations = locations;
        _facilities = facilities;
        _distance = distance;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<PagedResult<ListingCardDto>> Handle(
        SearchPropertiesQuery request, CancellationToken cancellationToken)
    {
        IdList.TryParse(request.FacilityIds, out var facilityIds);

        // Rule 85 runs before the cache is consulted, not after. A search naming an
        // id that does not exist has no cache entry and never will, so checking
        // afterwards would mean the refusal only ever arrives on a miss.
        await SearchFilters.RequirePlacesAsync(_locations, request.CityId, request.AreaId);
        await SearchFilters.RequireFacilitiesAsync(_facilities, facilityIds);
        var landmark = await SearchFilters.ResolveLandmarkAsync(_locations, request.LandmarkId);

        var criteria = new PropertySearchCriteria
        {
            CityId = request.CityId,
            AreaId = request.AreaId,
            LandmarkLatitude = landmark?.Latitude,
            LandmarkLongitude = landmark?.Longitude,
            MaxDistanceKm = request.MaxDistanceKm,
            MinRent = request.MinRent,
            MaxRent = request.MaxRent,
            RoomType = Parse<RoomType>(request.RoomType),
            GenderPolicy = Parse<GenderPolicy>(request.GenderPolicy),
            FacilityIds = facilityIds,
            AvailableOnly = request.AvailableOnly,
            Sort = Parse<PropertySort>(request.Sort) ?? PropertySort.Recent,
            Page = request.Page,
            PageSize = request.PageSize
        };

        var key = CacheKeys.PropertySearch(criteria.Describe());

        var cached = await _cache.GetAsync<PagedResult<ListingCardDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var listings = await _properties.SearchAsync(criteria);
        var measure = SearchFilters.Measure(_distance, landmark);

        var page = new PagedResult<ListingCardDto>
        {
            Items = listings.Select(listing =>
            {
                var card = _mapper.Map<ListingCardDto>(listing);
                card.DistanceKm = measure(listing);
                return card;
            }).ToList(),
            TotalCount = await _properties.CountSearchAsync(criteria),
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };

        await _cache.SetAsync(key, page, CacheKeys.SearchTtl);

        return page;
    }

    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        EnumName.TryParse<TEnum>(value, out var parsed) ? parsed : null;
}
