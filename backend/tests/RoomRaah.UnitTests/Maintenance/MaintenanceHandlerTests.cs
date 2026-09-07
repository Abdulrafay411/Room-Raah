using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Features.Maintenance.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Maintenance;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Maintenance;

public class MaintenanceHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IOtpRepository _codes = Substitute.For<IOtpRepository>();

    private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private SweepStaleListingsCommandHandler Sweep() =>
        new(_properties, _cache, _email, NullLogger<SweepStaleListingsCommandHandler>.Instance);

    private CleanUpExpiredOtpsCommandHandler Clean() => new(_codes);

    private static Property Stale(int id)
    {
        var property = TestData.Property(id, status: PropertyStatus.Published);

        property.Owner = TestData.User(1, $"owner{id}@roomraah.local", UserRole.Owner);
        property.AvailabilityConfirmedAt = Now.AddDays(-40);

        return property;
    }

    // ------------------------------------------------------------ stale sweep

    /// <summary>
    /// Rule 125's cutoff is thirty days back from the moment the sweep runs, and the
    /// moment is on the command so a test can put the boundary where it needs it
    /// without waiting a month for one.
    /// </summary>
    [Fact]
    public async Task The_sweep_asks_for_listings_unconfirmed_for_thirty_days()
    {
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([]);

        await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default);

        await _properties.Received(1).GetStaleAsync(
            Now - MaintenanceSettings.StaleAfter, MaintenanceSettings.StaleSweepBatchSize);
    }

    [Fact]
    public async Task A_stale_listing_earns_its_owner_a_nudge()
    {
        var property = Stale(1);
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([property]);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(1L);

        var sent = await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default);

        sent.ShouldBe(1);

        await _email.Received(1).SendStaleListingNudgeAsync(
            property.Owner, property.Title, property.AvailabilityConfirmedAt);
    }

    /// <summary>
    /// Rule 125's limit. The sweep runs daily and the email does not, or an owner away
    /// for a fortnight comes back to fourteen identical messages.
    /// </summary>
    [Fact]
    public async Task A_listing_nudged_this_week_is_not_nudged_again()
    {
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([Stale(1)]);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(2L);

        var sent = await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default);

        sent.ShouldBe(0);

        await _email.DidNotReceive().SendStaleListingNudgeAsync(
            Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task The_throttle_is_keyed_on_the_listing_for_seven_days()
    {
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([Stale(42)]);
        _cache.IncrementAsync(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(1L);

        await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default);

        await _cache.Received(1).IncrementAsync(
            CacheKeys.StaleNudge(42), MaintenanceSettings.StaleNudgeWindow);
    }

    /// <summary>
    /// The count is emails sent, not listings found. They are different numbers as soon
    /// as the throttle does anything, and the one worth logging is the one a person saw.
    /// </summary>
    [Fact]
    public async Task The_sweep_counts_the_emails_it_sent_and_not_the_listings_it_saw()
    {
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns([Stale(1), Stale(2), Stale(3)]);

        _cache.IncrementAsync(CacheKeys.StaleNudge(1), Arg.Any<TimeSpan>()).Returns(1L);
        _cache.IncrementAsync(CacheKeys.StaleNudge(2), Arg.Any<TimeSpan>()).Returns(4L);
        _cache.IncrementAsync(CacheKeys.StaleNudge(3), Arg.Any<TimeSpan>()).Returns(1L);

        var sent = await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default);

        sent.ShouldBe(2);
    }

    [Fact]
    public async Task A_sweep_that_finds_nothing_sends_nothing()
    {
        _properties.GetStaleAsync(Arg.Any<DateTime>(), Arg.Any<int>()).Returns([]);

        (await Sweep().Handle(new SweepStaleListingsCommand { AsOf = Now }, default)).ShouldBe(0);

        await _email.DidNotReceive().SendStaleListingNudgeAsync(
            Arg.Any<AppUser>(), Arg.Any<string>(), Arg.Any<DateTime>());
    }

    // ----------------------------------------------------------- otp cleanup

    /// <summary>Rule 126: seven days back, and the same cutoff for both halves.</summary>
    [Fact]
    public async Task The_cleanup_deletes_everything_older_than_the_retention_window()
    {
        _codes.DeleteExpiredAsync(Arg.Any<DateTime>()).Returns(12);

        var deleted = await Clean().Handle(new CleanUpExpiredOtpsCommand { AsOf = Now }, default);

        deleted.ShouldBe(12);

        await _codes.Received(1).DeleteExpiredAsync(Now - MaintenanceSettings.OtpRetention);
    }
}
