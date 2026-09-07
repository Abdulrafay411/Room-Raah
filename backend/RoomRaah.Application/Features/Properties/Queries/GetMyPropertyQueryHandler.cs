using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;

namespace RoomRaah.Application.Features.Properties.Queries;

public class GetMyPropertyQueryHandler : IRequestHandler<GetMyPropertyQuery, OwnerPropertyDto>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetMyPropertyQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<OwnerPropertyDto> Handle(GetMyPropertyQuery request, CancellationToken cancellationToken)
    {
        var property = OwnedListing.Require(
            await _properties.GetByIdAsync(request.PropertyId), request.PropertyId, request.OwnerId);

        return _mapper.Map<OwnerPropertyDto>(property);
    }
}
