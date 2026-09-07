using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;

namespace RoomRaah.Application.Features.Saved.Queries;

public class GetSavedPropertiesQueryHandler
    : IRequestHandler<GetSavedPropertiesQuery, PagedResult<SavedListingDto>>
{
    private readonly ISavedPropertyRepository _saved;
    private readonly IMapper _mapper;

    public GetSavedPropertiesQueryHandler(ISavedPropertyRepository saved, IMapper mapper)
    {
        _saved = saved;
        _mapper = mapper;
    }

    public async Task<PagedResult<SavedListingDto>> Handle(
        GetSavedPropertiesQuery request, CancellationToken cancellationToken)
    {
        var rows = await _saved.GetPageAsync(request.UserId, request.Page, request.PageSize);

        // Rule 99: a shortlist belongs to one person and is never cached, so nothing
        // here reads or writes Redis.
        var items = rows
            .Select(row =>
            {
                var dto = _mapper.Map<SavedListingDto>(row.Property);
                dto.SavedAt = row.SavedAt;
                return dto;
            })
            .ToList();

        return new PagedResult<SavedListingDto>
        {
            Items = items,
            TotalCount = await _saved.CountAsync(request.UserId),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
