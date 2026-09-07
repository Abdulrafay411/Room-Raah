using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reviews.Commands;

public class CreateReviewCommand : IRequest<MyReviewDto>
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int AuthorId { get; set; }

    public int PropertyId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    /// <summary>Visited, CurrentlyStaying or PreviouslyStayed.</summary>
    public string StayStatus { get; set; } = string.Empty;
}
