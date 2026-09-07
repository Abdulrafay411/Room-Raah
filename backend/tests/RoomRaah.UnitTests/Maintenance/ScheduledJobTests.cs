using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomRaah.Application.Features.Maintenance.Commands;
using RoomRaah.Infrastructure.BackgroundJobs;
using Shouldly;

namespace RoomRaah.UnitTests.Maintenance;

/// <summary>
/// Rule 128, asserted without a timer.
///
/// This is the whole reason one run of a job is a static method rather than something
/// buried inside a <c>BackgroundService</c>: a test that wants to know what a failing
/// job does should not have to start a host and wait for a schedule to come round.
/// </summary>
public class ScheduledJobTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly TestLogger _logger = new();

    [Fact]
    public async Task A_run_reports_what_the_job_processed()
    {
        _mediator.Send(Arg.Any<SweepStaleListingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(7);

        var count = await ScheduledJob.RunAsync(
            _mediator, new SweepStaleListingsCommand(), "StaleListingSweep", _logger);

        count.ShouldBe(7);

        _logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Information && e.Message.Contains("StaleListingSweep processed 7"));
    }

    /// <summary>
    /// Rule 128. A job that throws is logged and its timer keeps running — an exception
    /// escaping here stops the hosted service for the life of the process, so one bad
    /// night would switch the job off until somebody redeployed and nothing would say so.
    /// </summary>
    [Fact]
    public async Task A_job_that_throws_is_logged_and_does_not_escape()
    {
        _mediator.Send(Arg.Any<SweepStaleListingsCommand>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("the database went away"));

        var count = await ScheduledJob.RunAsync(
            _mediator, new SweepStaleListingsCommand(), "StaleListingSweep", _logger);

        count.ShouldBeNull();

        _logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Error && e.Message.Contains("StaleListingSweep failed"));
    }

    /// <summary>The failure carries the exception, because for a 500-shaped event the frames are the point.</summary>
    [Fact]
    public async Task A_failure_is_logged_with_the_exception()
    {
        _mediator.Send(Arg.Any<CleanUpExpiredOtpsCommand>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("boom"));

        await ScheduledJob.RunAsync(
            _mediator, new CleanUpExpiredOtpsCommand(), "ExpiredOtpCleanup", _logger);

        _logger.Entries.Single(e => e.Level == LogLevel.Error)
            .Exception.ShouldBeOfType<InvalidOperationException>();
    }

    private class TestLogger : ILogger
    {
        public record Entry(LogLevel Level, string Message, Exception? Exception);

        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
        }
    }
}
