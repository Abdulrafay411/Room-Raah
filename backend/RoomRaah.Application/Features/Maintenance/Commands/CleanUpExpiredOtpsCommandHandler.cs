using MediatR;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Maintenance;

namespace RoomRaah.Application.Features.Maintenance.Commands;

public class CleanUpExpiredOtpsCommandHandler : IRequestHandler<CleanUpExpiredOtpsCommand, int>
{
    private readonly IOtpRepository _codes;

    public CleanUpExpiredOtpsCommandHandler(IOtpRepository codes)
    {
        _codes = codes;
    }

    /// <summary>
    /// Rule 126. There is no unit of work here: the delete is one statement that runs
    /// when it is called, the same as marking a conversation read, and a SaveChanges
    /// after it would commit nothing while looking like it committed everything.
    /// </summary>
    public Task<int> Handle(CleanUpExpiredOtpsCommand request, CancellationToken cancellationToken)
    {
        return _codes.DeleteExpiredAsync(request.AsOf - MaintenanceSettings.OtpRetention);
    }
}
