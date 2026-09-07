using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CancelVisitCommand : IRequest<VisitRequestDto>
{
    public int VisitId { get; set; }

    /// <summary>Set from the token by the controller. Rule 91: the seeker who asked.</summary>
    public int SeekerId { get; set; }
}
