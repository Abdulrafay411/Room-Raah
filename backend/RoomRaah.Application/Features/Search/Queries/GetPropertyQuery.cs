using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>The public detail page. Rule 28: anything not published is a 404 here.</summary>
public class GetPropertyQuery : IRequest<ListingDetailDto>
{
    public int PropertyId { get; set; }
}
