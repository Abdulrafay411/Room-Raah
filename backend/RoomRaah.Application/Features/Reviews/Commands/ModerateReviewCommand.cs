using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reviews.Commands;

public class ModerateReviewCommand : IRequest<AdminReviewDto>
{
    public int ReviewId { get; set; }

    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int AdminId { get; set; }

    /// <summary>Approved or Hidden.</summary>
    public string State { get; set; } = string.Empty;
}
