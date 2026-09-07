using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Search.Queries;

/// <summary>The public gallery of one published listing. Rule 79.</summary>
public class GetPropertyPhotosQuery : IRequest<List<ListingPhotoDto>>
{
    public int PropertyId { get; set; }
}
