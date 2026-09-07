using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Persistence;

/// <summary>
/// §4.4 of the build plan, asserted line by line for everything a person does on the
/// site: visits, reviews, reports, conversations, inspections and badge events.
///
/// Every count here filters to the seeder's own listings by title, for the reason
/// <see cref="PropertySeedTests"/> gives: the endpoint tests in this collection create
/// activity of their own against the same database, and a count that included them
/// would pass or fail depending on the order the tests happened to run in.
/// </summary>
[Collection(ApiCollection.Name)]
public class ActivitySeedTests
{
    private readonly RoomRaahApiFactory _factory;

    public ActivitySeedTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task WithContextAsync(Func<RoomRaahDbContext, Task> assert)
    {
        using var scope = _factory.Services.CreateScope();
        await assert(scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>());
    }

    // ---------------------------------------------------------------- visits

    /// <summary>
    /// Nine, and one of every status. The count is nine rather than six because five
    /// reviews need five completed visits behind them — rule 38 — before the other four
    /// statuses are represented at all.
    /// </summary>
    [Fact]
    public async Task Every_visit_status_is_seeded()
    {
        await WithContextAsync(async context =>
        {
            var byStatus = await context.VisitRequests
                .IgnoreQueryFilters()
                .Where(v => PropertySeeder.SeededTitles.Contains(v.Property.Title))
                .GroupBy(v => v.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            byStatus.Values.Sum().ShouldBe(9);

            foreach (var status in Enum.GetValues<VisitStatus>())
            {
                byStatus.ShouldContainKey(status);
            }

            byStatus[VisitStatus.Completed].ShouldBe(5);
        });
    }

    /// <summary>
    /// Rule 90: a visit reaches `Completed` only once its preferred time has passed, and
    /// the validator refuses a future time — so a run starting from an empty database
    /// can never produce one by itself. Without a backdated accepted request, the whole
    /// review path is unreachable from a clean checkout.
    /// </summary>
    [Fact]
    public async Task An_accepted_visit_is_already_old_enough_to_complete()
    {
        await WithContextAsync(async context =>
        {
            var completable = await context.VisitRequests
                .IgnoreQueryFilters()
                .CountAsync(v => v.Status == VisitStatus.Accepted && v.PreferredAt < DateTime.UtcNow);

            completable.ShouldBeGreaterThan(0);
        });
    }

    /// <summary>Rule 89: a decline says why, or the seeker asks again.</summary>
    [Fact]
    public async Task The_declined_visit_carries_its_reason()
    {
        await WithContextAsync(async context =>
        {
            var declined = await context.VisitRequests
                .IgnoreQueryFilters()
                .Where(v => PropertySeeder.SeededTitles.Contains(v.Property.Title))
                .Where(v => v.Status == VisitStatus.Declined)
                .ToListAsync();

            declined.ShouldNotBeEmpty();
            declined.ShouldAllBe(v => v.OwnerResponseNote != null);
        });
    }

    // --------------------------------------------------------------- reviews

    [Fact]
    public async Task Five_reviews_cover_all_three_moderation_states()
    {
        await WithContextAsync(async context =>
        {
            var byState = await context.Reviews
                .IgnoreQueryFilters()
                .Where(r => PropertySeeder.SeededTitles.Contains(r.Property.Title))
                .GroupBy(r => r.ModerationState)
                .Select(g => new { State = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.State, x => x.Count);

            byState.Values.Sum().ShouldBe(5);
            byState[ModerationState.Approved].ShouldBe(3);
            byState[ModerationState.Pending].ShouldBe(1);
            byState[ModerationState.Hidden].ShouldBe(1);
        });
    }

    /// <summary>
    /// Rule 38, on the seed data itself. A review with no completed visit behind it is
    /// data that breaks a rule the product enforces: it looks right in the database and
    /// makes the first screen a lie.
    /// </summary>
    [Fact]
    public async Task Every_seeded_review_stands_on_a_completed_visit()
    {
        await WithContextAsync(async context =>
        {
            var reviews = await context.Reviews
                .IgnoreQueryFilters()
                .Where(r => PropertySeeder.SeededTitles.Contains(r.Property.Title))
                .Select(r => new { r.PropertyId, r.AuthorId })
                .ToListAsync();

            var completed = await context.VisitRequests
                .IgnoreQueryFilters()
                .Where(v => v.Status == VisitStatus.Completed)
                .Select(v => new { v.PropertyId, SeekerId = v.SeekerId })
                .ToListAsync();

            foreach (var review in reviews)
            {
                completed.ShouldContain(
                    v => v.PropertyId == review.PropertyId && v.SeekerId == review.AuthorId,
                    $"Review by {review.AuthorId} on property {review.PropertyId} has no completed visit.");
            }
        });
    }

    /// <summary>Rule 41: a moderated review records who moved it and when.</summary>
    [Fact]
    public async Task Every_moderated_review_names_the_admin_who_moved_it()
    {
        await WithContextAsync(async context =>
        {
            var moderated = await context.Reviews
                .IgnoreQueryFilters()
                .Where(r => PropertySeeder.SeededTitles.Contains(r.Property.Title))
                .Where(r => r.ModerationState != ModerationState.Pending)
                .ToListAsync();

            moderated.ShouldAllBe(r => r.ModeratedByAdminId != null && r.ModeratedAt != null);
        });
    }

    // --------------------------------------------------------------- reports

    [Fact]
    public async Task Two_reports_show_both_ends_of_the_queue()
    {
        await WithContextAsync(async context =>
        {
            var reports = await context.Reports
                .IgnoreQueryFilters()
                .Where(r => PropertySeeder.SeededTitles.Contains(r.Property.Title))
                .ToListAsync();

            reports.Count.ShouldBe(2);
            reports.Count(r => r.Status == ReportStatus.Open).ShouldBe(1);
            reports.Count(r => r.Status == ReportStatus.Upheld).ShouldBe(1);
        });
    }

    /// <summary>Rule 97: a verdict carries a note, and rule 98: it does not take the listing down.</summary>
    [Fact]
    public async Task The_upheld_report_carries_a_note_and_leaves_its_listing_published()
    {
        await WithContextAsync(async context =>
        {
            var upheld = await context.Reports
                .IgnoreQueryFilters()
                .Include(r => r.Property)
                .Where(r => PropertySeeder.SeededTitles.Contains(r.Property.Title))
                .SingleAsync(r => r.Status == ReportStatus.Upheld);

            upheld.AdminNote.ShouldNotBeNullOrWhiteSpace();
            upheld.ResolvedByAdminId.ShouldNotBeNull();
            upheld.Property.Status.ShouldBe(PropertyStatus.Published);
        });
    }

    // --------------------------------------------------------- conversations

    [Fact]
    public async Task Three_conversations_are_seeded_and_one_holds_something_unread()
    {
        await WithContextAsync(async context =>
        {
            var conversations = await context.Conversations
                .IgnoreQueryFilters()
                .Where(c => PropertySeeder.SeededTitles.Contains(c.Property.Title))
                .Select(c => new
                {
                    c.Id,
                    Messages = c.Messages.Count,
                    UnreadForSeeker = c.Messages.Count(m => m.SenderId != c.SeekerId && m.ReadAt == null)
                })
                .ToListAsync();

            conversations.Count.ShouldBe(3);
            conversations.ShouldAllBe(c => c.Messages > 0);
            conversations.Count(c => c.UnreadForSeeker > 0).ShouldBe(1);
        });
    }

    /// <summary>
    /// An unread badge that can only ever say one is a badge nobody can tell is
    /// counting rather than flagging.
    /// </summary>
    [Fact]
    public async Task The_unread_conversation_holds_more_than_one_unread_message()
    {
        await WithContextAsync(async context =>
        {
            var unread = await context.Conversations
                .IgnoreQueryFilters()
                .Where(c => PropertySeeder.SeededTitles.Contains(c.Property.Title))
                .Select(c => c.Messages.Count(m => m.SenderId != c.SeekerId && m.ReadAt == null))
                .ToListAsync();

            unread.Max().ShouldBeGreaterThan(1);
        });
    }

    // ------------------------------------------------- inspections and badge

    [Fact]
    public async Task Four_inspections_are_seeded_and_one_of_them_failed()
    {
        await WithContextAsync(async context =>
        {
            var inspections = await context.PropertyInspections
                .IgnoreQueryFilters()
                .Where(i => PropertySeeder.SeededTitles.Contains(i.Property.Title))
                .ToListAsync();

            inspections.Count.ShouldBe(4);
            inspections.Count(i => i.Result == InspectionResult.Passed).ShouldBe(3);
            inspections.Count(i => i.Result == InspectionResult.Failed).ShouldBe(1);
        });
    }

    /// <summary>Rule 115: an inspection is a record of a visit that happened.</summary>
    [Fact]
    public async Task No_seeded_inspection_is_dated_in_the_future()
    {
        await WithContextAsync(async context =>
        {
            var future = await context.PropertyInspections
                .IgnoreQueryFilters()
                .CountAsync(i => i.InspectedAt > DateTime.UtcNow);

            future.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Five_badge_events_are_seeded_and_they_show_both_directions()
    {
        await WithContextAsync(async context =>
        {
            var events = await context.BadgeEvents
                .IgnoreQueryFilters()
                .Where(e => PropertySeeder.SeededTitles.Contains(e.Property.Title))
                .ToListAsync();

            events.Count.ShouldBe(5);
            events.Count(e => e.Action == BadgeAction.Granted).ShouldBe(4);
            events.Count(e => e.Action == BadgeAction.Removed).ShouldBe(1);

            // Rule 112: a badge change that cannot say why is the thing §Business Model
            // calls worse than no badge at all.
            events.ShouldAllBe(e => e.Reason != string.Empty);
        });
    }

    /// <summary>
    /// A removal with no grant in front of it describes a badge taken off a listing that
    /// never had one — a history that contradicts itself, which is the one thing an
    /// audit trail must not do.
    /// </summary>
    [Fact]
    public async Task Every_removal_has_a_grant_before_it_on_the_same_listing()
    {
        await WithContextAsync(async context =>
        {
            var events = await context.BadgeEvents
                .IgnoreQueryFilters()
                .Select(e => new { e.PropertyId, e.Action, e.ActedAt })
                .ToListAsync();

            foreach (var removal in events.Where(e => e.Action == BadgeAction.Removed))
            {
                events.ShouldContain(
                    e => e.PropertyId == removal.PropertyId &&
                         e.Action == BadgeAction.Granted &&
                         e.ActedAt < removal.ActedAt,
                    $"Property {removal.PropertyId} has a badge removal with no grant before it.");
            }
        });
    }

    /// <summary>
    /// The trail and the listing have to agree. A listing carrying the badge whose last
    /// event was a removal — or the other way round — is seed data that would make the
    /// admin screen contradict the card next to it.
    /// </summary>
    [Fact]
    public async Task The_badge_on_a_listing_agrees_with_the_last_thing_that_happened_to_it()
    {
        await WithContextAsync(async context =>
        {
            var listings = await context.Properties
                .IgnoreQueryFilters()
                .Where(p => PropertySeeder.SeededTitles.Contains(p.Title))
                .Where(p => p.BadgeEvents.Any())
                .Select(p => new
                {
                    p.Id,
                    p.HasInspectionBadge,
                    Latest = p.BadgeEvents
                        .OrderByDescending(e => e.ActedAt)
                        .ThenByDescending(e => e.Id)
                        .Select(e => e.Action)
                        .First()
                })
                .ToListAsync();

            listings.ShouldNotBeEmpty();

            foreach (var listing in listings)
            {
                listing.HasInspectionBadge.ShouldBe(
                    listing.Latest == BadgeAction.Granted,
                    $"Property {listing.Id} carries {listing.HasInspectionBadge} "
                    + $"but its last badge event was {listing.Latest}.");
            }
        });
    }
}
