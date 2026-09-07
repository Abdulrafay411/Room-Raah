using MediatR;

namespace RoomRaah.Application.Features.Maintenance.Commands;

/// <summary>Rule 126.</summary>
public class CleanUpExpiredOtpsCommand : IRequest<int>
{
    /// <summary>The moment the cleanup runs; the retention window is measured back from it.</summary>
    public DateTime AsOf { get; set; } = DateTime.UtcNow;
}
