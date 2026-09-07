using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Locations.Queries;

public class SuggestLocationsQueryHandler
    : IRequestHandler<SuggestLocationsQuery, List<LocationSuggestionDto>>
{
    private const int CityOrder = 0;
    private const int AreaOrder = 1;
    private const int LandmarkOrder = 2;

    private readonly ILocationRepository _locationRepository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public SuggestLocationsQueryHandler(
        ILocationRepository locationRepository,
        ICacheService cache,
        IMapper mapper)
    {
        _locationRepository = locationRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<LocationSuggestionDto>> Handle(
        SuggestLocationsQuery request, CancellationToken cancellationToken)
    {
        var term = request.Q!.Trim().ToLowerInvariant();
        var key = CacheKeys.LocationSuggest(term);

        var cached = await _cache.GetAsync<List<LocationSuggestionDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var limit = SuggestLocationsQuery.MaxSuggestions;

        var cities = await _locationRepository.SearchCitiesAsync(term, limit);
        var areas = await _locationRepository.SearchAreasAsync(term, limit);
        var landmarks = await _locationRepository.SearchLandmarksAsync(term, limit);

        var candidates = cities.Select(c => Candidate(CityOrder, c))
            .Concat(areas.Select(a => Candidate(AreaOrder, a)))
            .Concat(landmarks.Select(l => Candidate(LandmarkOrder, l)));

        // Someone typing "nu" wants NUST above Bahria University, so a name that
        // starts with the term outranks one that merely contains it. Type order
        // and then the name break the remaining ties, which keeps the list stable.
        var suggestions = candidates
            .OrderBy(x => x.Suggestion.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.TypeOrder)
            .ThenBy(x => x.Suggestion.Name, StringComparer.OrdinalIgnoreCase)
            .Take(SuggestLocationsQuery.MaxSuggestions)
            .Select(x => x.Suggestion)
            .ToList();

        await _cache.SetAsync(key, suggestions, CacheKeys.ReferenceDataTtl);

        return suggestions;
    }

    private (int TypeOrder, LocationSuggestionDto Suggestion) Candidate<T>(int typeOrder, T source)
        where T : notnull
    {
        return (typeOrder, _mapper.Map<LocationSuggestionDto>(source));
    }
}
