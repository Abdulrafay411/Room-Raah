using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.AdminListings.Queries;

public class GetRevisionQueueQuery : IRequest<PagedResult<RevisionQueueRowDto>>
{
    /// <summary>
    /// Content or Availability. The split is the whole point of two queues: an
    /// availability row is worked in a batch in seconds, and putting it behind a
    /// field-by-field review is what makes owners stop confirming.
    /// </summary>
    public string? Kind { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
