using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Visits.Commands;

public class CreateVisitRequestCommand : IRequest<VisitRequestDto>
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int SeekerId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>Physical or Video.</summary>
    public string VisitType { get; set; } = string.Empty;

    public DateTime PreferredAt { get; set; }
}
