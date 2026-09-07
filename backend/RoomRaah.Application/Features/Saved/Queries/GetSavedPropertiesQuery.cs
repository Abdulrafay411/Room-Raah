using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Saved.Queries;

public class GetSavedPropertiesQuery : IRequest<PagedResult<SavedListingDto>>
{
    /// <summary>Set from the token by the controller, never from the query string.</summary>
    public int UserId { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
