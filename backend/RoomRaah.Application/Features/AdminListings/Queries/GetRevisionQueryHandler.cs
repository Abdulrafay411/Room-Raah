using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetRevisionQueryHandler : IRequestHandler<GetRevisionQuery, RevisionDetailDto>
{
    private readonly IPropertyRevisionRepository _revisions;
    private readonly IMapper _mapper;

    public GetRevisionQueryHandler(IPropertyRevisionRepository revisions, IMapper mapper)
    {
        _revisions = revisions;
        _mapper = mapper;
    }

    public async Task<RevisionDetailDto> Handle(GetRevisionQuery request, CancellationToken cancellationToken)
    {
        var revision = await _revisions.GetByIdAsync(request.RevisionId)
            ?? throw new NotFoundException(ReviewMessages.RevisionNotFound(request.RevisionId));

        return RevisionView.Build(revision, revision.Property, _mapper);
    }
}
