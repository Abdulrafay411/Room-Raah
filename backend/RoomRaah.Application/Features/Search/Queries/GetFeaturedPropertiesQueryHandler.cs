using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Search.Queries;

public class GetFeaturedPropertiesQueryHandler
    : IRequestHandler<GetFeaturedPropertiesQuery, List<ListingCardDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetFeaturedPropertiesQueryHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IMapper mapper)
    {
        _properties = properties;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<ListingCardDto>> Handle(
        GetFeaturedPropertiesQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.Featured();

        var cached = await _cache.GetAsync<List<ListingCardDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var listings = await _properties.GetFeaturedAsync(GetFeaturedPropertiesQuery.Count);

        // No landmark, so no distance: the landing page has not asked anyone where
        // they study yet.
        var cards = _mapper.Map<List<ListingCardDto>>(listings);

        await _cache.SetAsync(key, cards, CacheKeys.FeaturedTtl);

        return cards;
    }
}
