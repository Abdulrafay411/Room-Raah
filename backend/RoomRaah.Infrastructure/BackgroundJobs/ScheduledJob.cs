using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RoomRaah.Infrastructure.BackgroundJobs;

/// <summary>
/// One run of a background job: send the command, log what it did, and never let a
/// failure out.
///
/// This is a plain static method rather than something inside a <c>BackgroundService</c>
/// because rule 128 has to be provable. A test that wants to know what happens when a
/// job throws should not have to start a hosted service and wait for a timer to come
/// round; it calls this with a mediator that throws and reads the answer.
/// </summary>
public static class ScheduledJob
{
    /// <summary>
    /// Runs the command once. Returns the count it reported, or null if it failed —
    /// which is what the caller needs to know and the only thing a failure leaves
    /// behind, because rule 128 says the timer keeps going either way.
    /// </summary>
    public static async Task<int?> RunAsync(
        IMediator mediator, IRequest<int> command, string jobName, ILogger logger)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            var count = await mediator.Send(command);

            logger.LogInformation(
                "{JobName} processed {Count} records in {ElapsedMs}ms",
                jobName, count, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return count;
        }
        catch (Exception exception)
        {
            // Rule 128. Error, because unlike a refused request this is the case
            // nothing in the code anticipated — and the frames are the whole point.
            // Swallowed rather than rethrown, because an exception escaping here stops
            // the hosted service for the life of the process: one bad night would
            // silently switch the job off until somebody redeployed.
            logger.LogError(exception, "{JobName} failed", jobName);

            return null;
        }
    }
}
