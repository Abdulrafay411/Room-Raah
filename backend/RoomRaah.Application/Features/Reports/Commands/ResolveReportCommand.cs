using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Reports.Commands;

public class ResolveReportCommand : IRequest<ReportDto>
{
    public int ReportId { get; set; }

    /// <summary>Set from the token by the controller, never from the request body.</summary>
    public int AdminId { get; set; }

    /// <summary>UnderReview, Upheld or Dismissed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Rule 97: required on Upheld and on Dismissed.</summary>
    public string? AdminNote { get; set; }
}
