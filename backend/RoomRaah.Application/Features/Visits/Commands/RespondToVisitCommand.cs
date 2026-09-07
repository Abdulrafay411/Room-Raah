using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Visits.Commands;

public class RespondToVisitCommand : IRequest<VisitRequestDto>
{
    public int VisitId { get; set; }

    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int OwnerId { get; set; }

    /// <summary>Accepted or Declined.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Rule 89: required on a decline.</summary>
    public string? OwnerResponseNote { get; set; }
}
