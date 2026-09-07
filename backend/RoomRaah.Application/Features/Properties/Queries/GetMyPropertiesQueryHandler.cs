using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Properties.Queries;

public class GetMyPropertiesQueryHandler
    : IRequestHandler<GetMyPropertiesQuery, PagedResult<OwnerPropertySummaryDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetMyPropertiesQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<PagedResult<OwnerPropertySummaryDto>> Handle(
        GetMyPropertiesQuery request, CancellationToken cancellationToken)
    {
        var items = await _properties.GetByOwnerAsync(request.OwnerId, request.Page, request.PageSize);
        var total = await _properties.CountByOwnerAsync(request.OwnerId);

        return new PagedResult<OwnerPropertySummaryDto>
        {
            Items = _mapper.Map<List<OwnerPropertySummaryDto>>(items),
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
