namespace RoomRaah.Application.Maintenance;

/// <summary>
/// The numbers the two background jobs run on.
///
/// They are here rather than in an options class because they are product decisions
/// rather than deployment ones: rules 125 and 126 name them, and a value a deployment
/// could change is a value a rule cannot be tested against. How *often* a job runs is
/// a deployment question and lives in options; what "stale" means does not.
/// </summary>
public static class MaintenanceSettings
{
    /// <summary>
    /// Rule 125. Thirty days is not arbitrary: §Business Model says availability is
    /// re-confirmed monthly, so a listing that has passed a month without a
    /// confirmation is exactly the listing the freshness promise is about.
    /// </summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    /// <summary>
    /// Rule 125's limit. The sweep runs daily and the nudge does not, or an owner who
    /// is away for a fortnight comes back to fourteen identical emails and a filter
    /// rule that hides the fifteenth.
    /// </summary>
    public static readonly TimeSpan StaleNudgeWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// How many stale listings one sweep will look at.
    ///
    /// A sweep with no ceiling is fine until the day it is not, and the day it is not
    /// is the day it matters — a backlog that loads every stale listing at once is a
    /// job that stops finishing. Oldest first, so a capped run still makes progress
    /// through the backlog rather than circling the same rows.
    /// </summary>
    public const int StaleSweepBatchSize = 200;

    /// <summary>
    /// Rule 126. A consumed code is spent the moment it is used, but it is kept a week
    /// because it is the only record that a verification happened at all, and a support
    /// question about "I never got my code" arrives days later, not seconds.
    /// </summary>
    public static readonly TimeSpan OtpRetention = TimeSpan.FromDays(7);
}
