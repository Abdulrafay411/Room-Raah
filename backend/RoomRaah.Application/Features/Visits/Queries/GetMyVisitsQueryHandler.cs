using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Visits.Queries;

public class GetMyVisitsQueryHandler : IRequestHandler<GetMyVisitsQuery, PagedResult<VisitRequestDto>>
{
    private readonly IVisitRequestRepository _visits;
    private readonly IMapper _mapper;

    public GetMyVisitsQueryHandler(IVisitRequestRepository visits, IMapper mapper)
    {
        _visits = visits;
        _mapper = mapper;
    }

    public async Task<PagedResult<VisitRequestDto>> Handle(
        GetMyVisitsQuery request, CancellationToken cancellationToken)
    {
        var visits = await _visits.GetForSeekerAsync(request.SeekerId, request.Page, request.PageSize);

        return new PagedResult<VisitRequestDto>
        {
            Items = _mapper.Map<List<VisitRequestDto>>(visits),
            TotalCount = await _visits.CountForSeekerAsync(request.SeekerId),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
