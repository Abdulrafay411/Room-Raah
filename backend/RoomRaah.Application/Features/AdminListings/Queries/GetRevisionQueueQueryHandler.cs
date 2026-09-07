using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetRevisionQueueQueryHandler
    : IRequestHandler<GetRevisionQueueQuery, PagedResult<RevisionQueueRowDto>>
{
    private readonly IPropertyRevisionRepository _revisions;

    public GetRevisionQueueQueryHandler(IPropertyRevisionRepository revisions)
    {
        _revisions = revisions;
    }

    public async Task<PagedResult<RevisionQueueRowDto>> Handle(
        GetRevisionQueueQuery request, CancellationToken cancellationToken)
    {
        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? (RevisionKind?)null
            : Enum.Parse<RevisionKind>(request.Kind, ignoreCase: true);

        var pending = await _revisions.GetQueueAsync(kind, request.Page, request.PageSize);

        return new PagedResult<RevisionQueueRowDto>
        {
            Items = pending.Select(Row).ToList(),
            TotalCount = await _revisions.CountQueueAsync(kind),
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    /// <summary>
    /// A row carries enough to work an availability revision without opening it —
    /// the bed count now, the bed count asked for, and the total — and enough to
    /// decide whether a content revision is worth opening, which is the list of
    /// fields that moved rather than their values.
    /// </summary>
    private static RevisionQueueRowDto Row(Domain.Entities.PropertyRevision revision)
    {
        var property = revision.Property;

        return new RevisionQueueRowDto
        {
            Id = revision.Id,
            PropertyId = revision.PropertyId,
            PropertyTitle = property.Title,
            Kind = revision.Kind.ToString(),
            AreaName = property.Area.Name,
            CityName = property.Area.City.Name,
            OwnerName = property.Owner.FullName,
            ChangedFields = ProposedListing.From(property)
                .DifferencesFrom(ProposedListing.From(revision))
                .ToList(),
            CurrentAvailableBeds = property.AvailableBeds,
            ProposedAvailableBeds = revision.AvailableBeds,
            TotalBeds = property.TotalBeds,
            SubmittedAt = revision.SubmittedAt
        };
    }
}
