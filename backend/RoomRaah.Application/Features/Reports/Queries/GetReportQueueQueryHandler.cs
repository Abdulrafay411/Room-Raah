using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reports.Queries;

public class GetReportQueueQueryHandler : IRequestHandler<GetReportQueueQuery, PagedResult<ReportDto>>
{
    private readonly IReportRepository _reports;
    private readonly IMapper _mapper;

    public GetReportQueueQueryHandler(IReportRepository reports, IMapper mapper)
    {
        _reports = reports;
        _mapper = mapper;
    }

    public async Task<PagedResult<ReportDto>> Handle(
        GetReportQueueQuery request, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? (ReportStatus?)null
            : Enum.Parse<ReportStatus>(request.Status, ignoreCase: true);

        var reports = await _reports.GetQueueAsync(status, request.Page, request.PageSize);

        return new PagedResult<ReportDto>
        {
            Items = _mapper.Map<List<ReportDto>>(reports),
            TotalCount = await _reports.CountQueueAsync(status),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
