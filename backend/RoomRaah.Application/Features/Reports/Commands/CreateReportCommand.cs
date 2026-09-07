using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reports.Commands;

public class CreateReportCommand : IRequest<MyReportDto>
{
    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int ReportedByUserId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>Inaccurate, Misleading, AlreadyTaken, Scam, Offensive or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;
}
