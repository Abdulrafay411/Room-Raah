using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.Reviews.Queries;

public class GetReviewQueueQueryHandler
    : IRequestHandler<GetReviewQueueQuery, PagedResult<AdminReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly IMapper _mapper;

    public GetReviewQueueQueryHandler(IReviewRepository reviews, IMapper mapper)
    {
        _reviews = reviews;
        _mapper = mapper;
    }

    public async Task<PagedResult<AdminReviewDto>> Handle(
        GetReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var state = string.IsNullOrWhiteSpace(request.State)
            ? (ModerationState?)null
            : Enum.Parse<ModerationState>(request.State, ignoreCase: true);

        var reviews = await _reviews.GetQueueAsync(state, request.Page, request.PageSize);

        return new PagedResult<AdminReviewDto>
        {
            Items = _mapper.Map<List<AdminReviewDto>>(reviews),
            TotalCount = await _reviews.CountQueueAsync(state),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
