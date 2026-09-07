using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Search.Queries;

public class GetPropertyPhotosQueryHandler
    : IRequestHandler<GetPropertyPhotosQuery, List<ListingPhotoDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly ICacheService _cache;
    private readonly IMapper _mapper;

    public GetPropertyPhotosQueryHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IMapper mapper)
    {
        _properties = properties;
        _cache = cache;
        _mapper = mapper;
    }

    public async Task<List<ListingPhotoDto>> Handle(
        GetPropertyPhotosQuery request, CancellationToken cancellationToken)
    {
        var key = CacheKeys.PropertyPhotos(request.PropertyId);

        var cached = await _cache.GetAsync<List<ListingPhotoDto>>(key);
        if (cached is not null)
        {
            return cached;
        }

        var listing = await _properties.GetPublishedAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        var photos = _mapper.Map<List<ListingPhotoDto>>(ListingGallery.Public(listing));

        await _cache.SetAsync(key, photos, CacheKeys.DetailTtl);

        return photos;
    }
}
