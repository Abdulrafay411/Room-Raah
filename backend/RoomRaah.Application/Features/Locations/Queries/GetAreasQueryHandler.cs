using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Locations.Queries;

public class GetAreasQueryHandler : IRequestHandler<GetAreasQuery, List<AreaDto>>
{
    private readonly ILocationRepository _locationRepository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetAreasQueryHandler(
        ILocationRepository locationRepository,
        ICacheService cache,
        IMapper mapper)
    {
        _locationRepository = locationRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<AreaDto>> Handle(GetAreasQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.Areas(request.CityId);

        // Only a city that exists is ever written to the cache, so a hit is proof
        // enough that it does — the existence check belongs on the miss path only.
        var cached = await _cache.GetAsync<List<AreaDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        if (!await _locationRepository.CityExistsAsync(request.CityId))
        {
            throw new NotFoundException($"City {request.CityId} was not found.");
        }

        var areas = _mapper.Map<List<AreaDto>>(await _locationRepository.GetAreasAsync(request.CityId));

        await _cache.SetAsync(key, areas, CacheKeys.ReferenceDataTtl);

        return areas;
    }
}
