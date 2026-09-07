using MediatR;

namespace RoomRaah.Application.Features.Maintenance.Commands;

/// <summary>
/// Rule 125. Sent by a timer rather than by a controller, and otherwise an ordinary
/// command — which is the whole point of rule 127: the work is here, where a test can
/// send it, and the hosted service only decides when.
/// </summary>
public class SweepStaleListingsCommand : IRequest<int>
{
    /// <summary>
    /// The moment the sweep runs. Passed in rather than read from the clock inside the
    /// handler, so a test can put the boundary wherever it needs it without waiting a
    /// month for one.
    /// </summary>
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}
