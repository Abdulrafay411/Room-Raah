using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Inspections.Queries;

public class GetDueForInspectionQueryHandler
    : IRequestHandler<GetDueForInspectionQuery, PagedResult<DueForInspectionDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly IMapper _mapper;

    public GetDueForInspectionQueryHandler(IPropertyRepository properties, IMapper mapper)
    {
        _properties = properties;
        _mapper = mapper;
    }

    public async Task<PagedResult<DueForInspectionDto>> Handle(
        GetDueForInspectionQuery request, CancellationToken cancellationToken)
    {
        var cutoff = BadgeRules.DueCutoff(request.AsOf);

        var due = await _properties.GetDueForInspectionAsync(cutoff, request.Page, request.PageSize);

        var items = due.Select(property =>
        {
            var dto = _mapper.Map<DueForInspectionDto>(property);

            // Counted from the last visit, because there is no next one to count to.
            // A badged listing that has somehow never been inspected is as overdue as
            // the interval itself, rather than crashing on a null.
            var since = property.LastInspectionAt ?? cutoff;

            dto.DaysOverdue = Math.Max(0, (int)(request.AsOf - since).TotalDays
                                          - (int)BadgeRules.ReinspectionInterval.TotalDays);

            return dto;
        }).ToList();

        return new PagedResult<DueForInspectionDto>
        {
            Items = items,
            TotalCount = await _properties.CountDueForInspectionAsync(cutoff),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
