using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetListingForReviewQueryHandler : IRequestHandler<GetListingForReviewQuery, AdminPropertyDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetListingForReviewQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<AdminPropertyDto> Handle(
        GetListingForReviewQuery request, CancellationToken cancellationToken)
    {
        var property = await _properties.GetForReviewAsync(request.PropertyId)
            ?? throw new NotFoundException(PropertyMessages.PropertyNotFound(request.PropertyId));

        var dto = _mapper.Map<AdminPropertyDto>(property);

        // Answered here rather than left to the panel to work out, so that the rule
        // the approve endpoint enforces and the button the admin sees are the same
        // rule read from the same place.
        dto.CanPublish = ListingReview.ChecksPass(property);

        return dto;
    }
}
