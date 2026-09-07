using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Inspections.Commands;

/// <summary>
/// Rule 108. §5 named no endpoint for this at all, so the rule guarded an act nobody
/// could perform — the same shape as rule 38 before Phase 6 and rule 43 before 7A.
/// </summary>
public class RequestInspectionCommand : IRequest<OwnerInspectionStateDto>
{
    /// <summary>Set from the route by the controller.</summary>
    public int PropertyId { get; set; }

    /// <summary>Set from the token by the controller.</summary>
    public int OwnerId { get; set; }
}
