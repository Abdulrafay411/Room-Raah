using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Visits.Commands;

/// <summary>
/// Rule 90. Either side may say the visit happened, and the reason is rule 38: a
/// review needs a completed visit, so an owner who alone could complete one would
/// hold a veto over every review of their own property.
/// </summary>
public class CompleteVisitCommand : IRequest<VisitRequestDto>
{
    public int VisitId { get; set; }

    /// <summary>Set from the token by the controller. Either participant.</summary>
    public int UserId { get; set; }
}
