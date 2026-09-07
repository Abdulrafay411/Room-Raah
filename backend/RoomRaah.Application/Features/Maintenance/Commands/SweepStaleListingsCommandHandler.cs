using MediatR;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Maintenance;

namespace RoomRaah.Application.Features.Maintenance.Commands;

public class SweepStaleListingsCommandHandler : IRequestHandler<SweepStaleListingsCommand, int>
{
    private readonly IPropertyRepository _properties;
    private readonly ICacheService _cache;
    private readonly IEmailSender _email;
    private readonly ILogger<SweepStaleListingsCommandHandler> _logger;

    public SweepStaleListingsCommandHandler(
        IPropertyRepository properties,
        ICacheService cache,
        IEmailSender email,
        ILogger<SweepStaleListingsCommandHandler> logger)
    {
        _properties = properties;
        _cache = cache;
        _email = email;
        _logger = logger;
    }

    /// <summary>
    /// Rule 125. Answers with how many nudges actually went out, not how many stale
    /// listings there were — the throttle means those are different numbers, and the
    /// one worth logging is the number of emails a person received.
    /// </summary>
    public async Task<int> Handle(SweepStaleListingsCommand request, CancellationToken cancellationToken)
    {
        var cutoff = request.AsOf - MaintenanceSettings.StaleAfter;

        var stale = await _properties.GetStaleAsync(cutoff, MaintenanceSettings.StaleSweepBatchSize);

        var sent = 0;

        foreach (var property in stale)
        {
            // The same shape as the message nudge: a counter rather than a get-then-set,
            // so two runs overlapping cannot both decide they are the first.
            var count = await _cache.IncrementAsync(
                CacheKeys.StaleNudge(property.Id), MaintenanceSettings.StaleNudgeWindow);

            if (count > 1)
            {
                continue;
            }

            await _email.SendStaleListingNudgeAsync(
                property.Owner, property.Title, property.AvailabilityConfirmedAt);

            sent++;
        }

        _logger.LogDebug(
            "StaleListingSweep found {Found} stale listings and nudged {Sent}",
            stale.Count, sent);

        return sent;
    }
}
