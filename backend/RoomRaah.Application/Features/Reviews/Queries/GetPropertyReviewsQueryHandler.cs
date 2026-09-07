using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Reviews.Queries;

public class GetPropertyReviewsQueryHandler
    : IRequestHandler<GetPropertyReviewsQuery, PropertyReviewsDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IReviewRepository _reviews;
    private readonly IMapper _mapper;

    public GetPropertyReviewsQueryHandler(
        IPropertyRepository properties, IReviewRepository reviews, IMapper mapper)
    {
        _properties = properties;
        _reviews = reviews;
        _mapper = mapper;
    }

    public async Task<PropertyReviewsDto> Handle(
        GetPropertyReviewsQuery request, CancellationToken cancellationToken)
    {
        // Rule 28. Asked before the reviews are read, so that the reviews of a listing
        // an admin has taken down are not readable through a route that never looks at
        // the listing itself.
        var property = await _properties.GetPublishedWithOwnerAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        var reviews = await _reviews.GetApprovedAsync(property.Id, request.Page, request.PageSize);

        // Rule 99. Public and still not cached: an approval would have to evict this,
        // and it is one indexed read per listing view. The eviction would cost more
        // than the cache saved.
        return new PropertyReviewsDto
        {
            Items = _mapper.Map<List<ReviewDto>>(reviews),
            TotalCount = await _reviews.CountApprovedAsync(property.Id),

            // Rule 94: over every approved review, not over this page. An average that
            // changed as a reader paged would not be an average of anything.
            AverageRating = await _reviews.AverageApprovedAsync(property.Id),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
