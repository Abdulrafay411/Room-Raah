using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Inspections.Commands;

public class RecordInspectionCommand : IRequest<PropertyInspectionDto>
{
    /// <summary>Set from the token by the controller.</summary>
    public int AdminId { get; set; }

    public int PropertyId { get; set; }

    /// <summary>
    /// When the visit happened. Nullable so that "missing" and "the first moment of
    /// year one" stay different answers — an absent DateTime binds to a real date, and
    /// a required field that can never be absent is not required at all.
    /// </summary>
    public DateTime? InspectedAt { get; set; }

    /// <summary>Passed or Failed.</summary>
    public string Result { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public decimal? FeeAmount { get; set; }

    public DateTime? FeeCollectedAt { get; set; }
}
