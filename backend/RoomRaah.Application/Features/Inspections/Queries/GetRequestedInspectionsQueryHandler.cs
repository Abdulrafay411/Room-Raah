using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Inspections.Queries;

public class GetRequestedInspectionsQueryHandler
    : IRequestHandler<GetRequestedInspectionsQuery, PagedResult<RequestedInspectionDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetRequestedInspectionsQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<PagedResult<RequestedInspectionDto>> Handle(
        GetRequestedInspectionsQuery request, CancellationToken cancellationToken)
    {
        var requested = await _properties.GetInspectionRequestedAsync(request.Page, request.PageSize);

        // Not cached, for the same reason as every admin queue: it is changed by the
        // decisions of the people reading it.
        return new PagedResult<RequestedInspectionDto>
        {
            Items = _mapper.Map<List<RequestedInspectionDto>>(requested),
            TotalCount = await _properties.CountInspectionRequestedAsync(),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
