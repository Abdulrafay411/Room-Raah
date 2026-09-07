using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Locations.Queries;

public class GetCitiesQueryHandler : IRequestHandler<GetCitiesQuery, List<CityDto>>
{
    private readonly ILocationRepository _locationRepository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetCitiesQueryHandler(
        ILocationRepository locationRepository,
        ICacheService cache,
        IMapper mapper)
    {
        _locationRepository = locationRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<CityDto>> Handle(GetCitiesQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.Cities();

        var cached = await _cache.GetAsync<List<CityDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var cities = _mapper.Map<List<CityDto>>(await _locationRepository.GetCitiesAsync());

        await _cache.SetAsync(key, cities, CacheKeys.ReferenceDataTtl);

        return cities;
    }
}
