namespace RoomRaah.Infrastructure.BackgroundJobs;

/// <summary>
/// How often the jobs run, and whether they run at all.
///
/// *How often* is a deployment question and belongs here. *What stale means* is a
/// product decision, lives in <c>MaintenanceSettings</c>, and is not configurable —
/// a threshold a deployment can change is a threshold a rule cannot be tested against.
/// </summary>
public class BackgroundJobOptions
{
    /// <summary>
    /// Off in the integration suite, on everywhere else.
    ///
    /// A timer firing in the middle of a test run would send emails no test asked for
    /// and make every email count in every other suite depend on how long the run took.
    /// Rule 127 is what makes turning them off honest rather than convenient: the work
    /// is in a handler and the tests send the command, so what is switched off here is
    /// only the clock.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to wait before the first run. Long enough that a job never competes
    /// with startup migration and seeding for the same database.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Daily. The nudge itself is throttled to one a week per listing, so running more
    /// often than this buys nothing, and running less often means a listing can sit
    /// stale for over a month before anybody is told.
    /// </summary>
    public TimeSpan StaleSweepInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Daily. Nothing depends on the timing; the rows are already useless.</summary>
    public TimeSpan OtpCleanupInterval { get; set; } = TimeSpan.FromHours(24);
}
