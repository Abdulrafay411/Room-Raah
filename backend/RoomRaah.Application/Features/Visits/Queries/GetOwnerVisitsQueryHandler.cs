using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Visits.Queries;

public class GetOwnerVisitsQueryHandler
    : IRequestHandler<GetOwnerVisitsQuery, PagedResult<VisitRequestDto>>
{
    private readonly IVisitRequestRepository _visits;
    private readonly IMapper _mapper;

    public GetOwnerVisitsQueryHandler(IVisitRequestRepository visits, IMapper mapper)
    {
        _visits = visits;
        _mapper = mapper;
    }

    public async Task<PagedResult<VisitRequestDto>> Handle(
        GetOwnerVisitsQuery request, CancellationToken cancellationToken)
    {
        var visits = await _visits.GetForOwnerAsync(request.OwnerId, request.Page, request.PageSize);

        return new PagedResult<VisitRequestDto>
        {
            Items = _mapper.Map<List<VisitRequestDto>>(visits),
            TotalCount = await _visits.CountForOwnerAsync(request.OwnerId),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
