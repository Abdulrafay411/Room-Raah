using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.Features.Maintenance.Commands;
using RoomRaah.Application.Maintenance;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Maintenance;

/// <summary>
/// Both jobs against the real database, by sending the command the timer would send.
///
/// Rule 127 is what makes this the whole test rather than half of it: the hosted service
/// decides only *when*, and every line of *what* is here. Nothing in this file waits.
/// </summary>
[Collection(ApiCollection.Name)]
public class BackgroundJobTests
{
    private readonly RoomRaahApiFactory _factory;

    public BackgroundJobTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<T> SendAsync<T>(IRequest<T> command)
    {
        using var scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    /// <summary>
    /// Moves a listing's last confirmation into the past. Written against the database
    /// because the passage of a month is not something the API can be asked for — the
    /// same reason a visit request has to be backdated to be completable.
    /// </summary>
    private async Task ConfirmedAtAsync(int propertyId, DateTime confirmedAt)
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        var property = await context.Properties.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == propertyId);

        property.AvailabilityConfirmedAt = confirmedAt;

        await context.SaveChangesAsync();
    }

    // ------------------------------------------------------------ stale sweep

    [Fact]
    public async Task A_listing_unconfirmed_for_over_a_month_earns_one_nudge_and_not_two()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "sweep-stale");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);

        await ConfirmedAtAsync(listing.Id, DateTime.UtcNow.AddDays(-45));

        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(1);

        // Rule 125's limit: the sweep runs daily, the email does not.
        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(1);
    }

    /// <summary>
    /// A freshly approved listing is stamped by rule 24, so it is exactly the case that
    /// must not be nudged — otherwise every owner is chased the day they are published.
    /// </summary>
    [Fact]
    public async Task A_listing_confirmed_recently_is_left_alone()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "sweep-fresh");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        await owner.CreatePublishedAsync(admin);

        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(0);
    }

    /// <summary>
    /// The boundary itself. Twenty-nine days is not stale and thirty-one is, and a test
    /// that only checks forty-five days would pass with the threshold set to anything.
    /// </summary>
    [Fact]
    public async Task The_threshold_is_thirty_days_and_not_a_direction()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "sweep-edge");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);

        await ConfirmedAtAsync(listing.Id, DateTime.UtcNow.AddDays(-29));

        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(0);

        await ConfirmedAtAsync(listing.Id, DateTime.UtcNow.AddDays(-31));

        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(1);
    }

    /// <summary>
    /// Only published listings. A draft has no availability anybody is relying on, and
    /// chasing an owner about a listing that is not on the site is noise.
    /// </summary>
    [Fact]
    public async Task A_draft_is_never_nudged_however_old_it_is()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "sweep-draft");
        var draft = await owner.CreateDraftAsync();

        await ConfirmedAtAsync(draft.Id, DateTime.UtcNow.AddDays(-400));

        await SendAsync(new SweepStaleListingsCommand { AsOf = DateTime.UtcNow });

        _factory.Emails.DecisionsFor(owner.Email)
            .Count(d => d.Decision == "StaleListing").ShouldBe(0);
    }

    // ----------------------------------------------------------- otp cleanup

    [Fact]
    public async Task Codes_that_expired_or_were_used_more_than_a_week_ago_are_deleted()
    {
        var email = $"cleanup.{Guid.NewGuid():N}@roomraah.local";
        var client = _factory.CreateClient();

        await client.RegisterAndVerifyAsync(_factory, email);

        var userId = await UserIdAsync(email);

        // Three rows, chosen so the cutoff has to be applied to both halves of rule 126
        // and to neither of the two that are still inside the window.
        await AddCodesAsync(
            Expired(userId, DateTime.UtcNow.AddDays(-10)),
            Consumed(userId, DateTime.UtcNow.AddDays(-10)),
            Consumed(userId, DateTime.UtcNow.AddHours(-1)));

        var before = await CodeCountAsync(userId);

        await SendAsync(new CleanUpExpiredOtpsCommand { AsOf = DateTime.UtcNow });

        var after = await CodeCountAsync(userId);

        (before - after).ShouldBe(2);
    }

    /// <summary>
    /// A code consumed a minute ago is spent, but it is also the only record that a
    /// verification happened — and the support question about it arrives days later.
    /// </summary>
    [Fact]
    public async Task A_code_used_a_minute_ago_survives_the_cleanup()
    {
        var email = $"cleanup.recent.{Guid.NewGuid():N}@roomraah.local";
        var client = _factory.CreateClient();

        await client.RegisterAndVerifyAsync(_factory, email);

        var userId = await UserIdAsync(email);

        await AddCodesAsync(Consumed(userId, DateTime.UtcNow.AddMinutes(-1)));

        await SendAsync(new CleanUpExpiredOtpsCommand { AsOf = DateTime.UtcNow });

        (await CodeCountAsync(userId)).ShouldBeGreaterThan(0);
    }

    // ------------------------------------------------------------- fixtures

    private static OtpCode Expired(int userId, DateTime expiresAt) => new()
    {
        AppUserId = userId,
        Purpose = OtpPurpose.VerifyEmail,
        CodeHash = "not-a-real-hash",
        ExpiresAt = expiresAt
    };

    private static OtpCode Consumed(int userId, DateTime consumedAt) => new()
    {
        AppUserId = userId,
        Purpose = OtpPurpose.VerifyEmail,
        CodeHash = "not-a-real-hash",

        // Deliberately still in date, so only ConsumedAt can decide this row's fate.
        ExpiresAt = DateTime.UtcNow.AddYears(1),
        ConsumedAt = consumedAt
    };

    private async Task AddCodesAsync(params OtpCode[] codes)
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        context.OtpCodes.AddRange(codes);

        await context.SaveChangesAsync();
    }

    private async Task<int> UserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        return await context.AppUsers.IgnoreQueryFilters()
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .FirstAsync();
    }

    private async Task<int> CodeCountAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        return await context.OtpCodes.IgnoreQueryFilters()
            .CountAsync(c => c.AppUserId == userId);
    }
}
