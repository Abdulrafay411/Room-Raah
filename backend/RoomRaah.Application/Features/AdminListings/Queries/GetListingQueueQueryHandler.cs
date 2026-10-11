using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetListingQueueQueryHandler
    : IRequestHandler<GetListingQueueQuery, PagedResult<AdminPropertySummaryDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetListingQueueQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<PagedResult<AdminPropertySummaryDto>> Handle(
        GetListingQueueQuery request, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? (PropertyStatus?)null
            : Enum.Parse<PropertyStatus>(request.Status, ignoreCase: true);

        var search = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();

        var listings = await _properties.GetByStatusAsync(status, search, request.Page, request.PageSize);

        // Not cached, and never will be. A review queue is unpublished work read by a
        // handful of people and changed by every decision they make.
        return new PagedResult<AdminPropertySummaryDto>
        {
            Items = _mapper.Map<List<AdminPropertySummaryDto>>(listings),
            TotalCount = await _properties.CountByStatusAsync(status, search),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
