using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Search.Queries;

public class GetPropertyQueryHandler : IRequestHandler<GetPropertyQuery, ListingDetailDto>
{
    private readonly IPropertyRepository _properties;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetPropertyQueryHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IMapper mapper)
    {
        _properties = properties;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<ListingDetailDto> Handle(
        GetPropertyQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.PropertyDetail(request.PropertyId);

        var cached = await _cache.GetAsync<ListingDetailDto>(key);
        if (cached is not null)
        {
            return cached;
        }

        // A miss is not cached. An id that does not resolve today is usually an id
        // that is about to — a listing an admin is minutes away from approving — and
        // a cached 404 would hold it back for a quarter of an hour after that.
        var listing = await _properties.GetPublishedAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        var detail = _mapper.Map<ListingDetailDto>(listing);

        await _cache.SetAsync(key, detail, CacheKeys.DetailTtl);

        return detail;
    }
}
