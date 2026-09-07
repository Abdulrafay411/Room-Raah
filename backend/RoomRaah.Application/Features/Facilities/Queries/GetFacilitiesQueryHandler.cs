using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Facilities.Queries;

public class GetFacilitiesQueryHandler : IRequestHandler<GetFacilitiesQuery, List<FacilityDto>>
{
    private readonly IFacilityRepository _facilityRepository;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetFacilitiesQueryHandler(
        IFacilityRepository facilityRepository,
        ICacheService cache,
        IMapper mapper)
    {
        _facilityRepository = facilityRepository;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<FacilityDto>> Handle(
        GetFacilitiesQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.Facilities();

        var cached = await _cache.GetAsync<List<FacilityDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var facilities = _mapper.Map<List<FacilityDto>>(await _facilityRepository.GetAllAsync());

        await _cache.SetAsync(key, facilities, CacheKeys.ReferenceDataTtl);

        return facilities;
    }
}
