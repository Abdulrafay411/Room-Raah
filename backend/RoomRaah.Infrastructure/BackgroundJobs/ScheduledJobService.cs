using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Features.Maintenance.Commands;

namespace RoomRaah.Infrastructure.BackgroundJobs;

/// <summary>
/// The clock, and nothing else. Rule 127: it opens a scope on its timer and sends a
/// command, exactly the way a controller would, so every line of the work it triggers
/// is tested by sending that command instead of by waiting.
///
/// There is nothing here to get wrong on purpose, which is the point — everything that
/// could be wrong is in the handler or in <c>ScheduledJob</c>, and both are reachable
/// from a test without a timer.
/// </summary>
public abstract class ScheduledJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger _logger;

    protected ScheduledJobService(
        IServiceScopeFactory scopes, IOptions<BackgroundJobOptions> options, ILogger logger)
    {
        _scopes = scopes;
        Options = options.Value;
        _logger = logger;
    }

    protected BackgroundJobOptions Options { get; }

    protected abstract string JobName { get; }

    protected abstract TimeSpan Interval { get; }

    /// <summary>Built per run, so the command carries the time the run actually started.</summary>
    protected abstract IRequest<int> BuildCommand();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Options.Enabled)
        {
            _logger.LogInformation("{JobName} is disabled", JobName);
            return;
        }

        try
        {
            await Task.Delay(Options.StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _scopes.CreateScope())
                {
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await ScheduledJob.RunAsync(mediator, BuildCommand(), JobName, _logger);
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The host is stopping. Not a failure, and not something to log as one.
        }
    }
}

public class StaleListingSweepService : ScheduledJobService
{
    public StaleListingSweepService(
        IServiceScopeFactory scopes,
        IOptions<BackgroundJobOptions> options,
        ILogger<StaleListingSweepService> logger)
        : base(scopes, options, logger)
    {
    }

    protected override string JobName => "StaleListingSweep";

    protected override TimeSpan Interval => Options.StaleSweepInterval;

    protected override IRequest<int> BuildCommand() =>
        new SweepStaleListingsCommand { AsOf = DateTime.UtcNow };
}

public class ExpiredOtpCleanupService : ScheduledJobService
{
    public ExpiredOtpCleanupService(
        IServiceScopeFactory scopes,
        IOptions<BackgroundJobOptions> options,
        ILogger<ExpiredOtpCleanupService> logger)
        : base(scopes, options, logger)
    {
    }

    protected override string JobName => "ExpiredOtpCleanup";

    protected override TimeSpan Interval => Options.OtpCleanupInterval;

    protected override IRequest<int> BuildCommand() =>
        new CleanUpExpiredOtpsCommand { AsOf = DateTime.UtcNow };
}
