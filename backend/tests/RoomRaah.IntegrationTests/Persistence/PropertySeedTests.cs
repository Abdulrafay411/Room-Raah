using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Persistence;

/// <summary>
/// The seeded listings have to demonstrate every status and both ends of the
/// availability rules, because the admin and search phases are built against them.
/// </summary>
[Collection(ApiCollection.Name)]
public class PropertySeedTests
{
    private const double SouthernMost = 23.5;
    private const double NorthernMost = 37.1;
    private const double WesternMost = 60.9;
    private const double EasternMost = 77.9;

    private const int StaleAfterDays = 30;

    private readonly RoomRaahApiFactory _factory;

    public PropertySeedTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task WithContextAsync(Func<RoomRaahDbContext, Task> assert)
    {
        using var scope = _factory.Services.CreateScope();
        await assert(scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>());
    }

    /// <summary>
    /// Only the listings the seeder wrote. Endpoint tests in this collection create
    /// listings of their own against the same database, and a count that included
    /// them would pass or fail depending on the order the tests happened to run in.
    /// </summary>
    private static IQueryable<Property> Seeded(RoomRaahDbContext context)
    {
        return context.Properties
            .IgnoreQueryFilters()
            .Where(p => PropertySeeder.SeededTitles.Contains(p.Title));
    }

    private static IQueryable<PropertyPhoto> SeededPhotos(RoomRaahDbContext context)
    {
        return context.PropertyPhotos
            .IgnoreQueryFilters()
            .Where(p => PropertySeeder.SeededTitles.Contains(p.Property.Title));
    }

    /// <summary>
    /// The assertions below tell the seeder's listings from a test's by title, so a
    /// fixture that borrowed a seeded title would be counted as seed data — and
    /// would only start failing once enough tests existed to push a count past its
    /// expected value, which is exactly how it was found.
    /// </summary>
    [Fact]
    public void A_test_fixture_never_borrows_a_seeded_title()
    {
        PropertySeeder.SeededTitles.ShouldNotContain(OwnerWorkspace.FixtureTitle);
    }

    /// <summary>
    /// The same guard for the map. A fixture standing exactly where a seeded listing
    /// stands makes every distance assertion true for the wrong reason, and the test
    /// that noticed had been passing for three phases.
    /// </summary>
    [Fact]
    public async Task A_test_fixture_never_stands_where_a_seeded_listing_stands()
    {
        await WithContextAsync(async context =>
        {
            var collisions = await Seeded(context)
                .Where(p => p.Latitude == OwnerWorkspace.FixtureLatitude &&
                            p.Longitude == OwnerWorkspace.FixtureLongitude)
                .Select(p => p.Title)
                .ToListAsync();

            collisions.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task Every_status_in_the_build_plan_is_represented()
    {
        await WithContextAsync(async context =>
        {
            var seeded = await Seeded(context)
                .GroupBy(p => p.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            seeded[PropertyStatus.Published].ShouldBe(8);
            seeded[PropertyStatus.PendingReview].ShouldBe(2);
            seeded[PropertyStatus.ChangesRequested].ShouldBe(1);
            seeded[PropertyStatus.Rejected].ShouldBe(1);
            seeded[PropertyStatus.Draft].ShouldBe(1);
            seeded[PropertyStatus.Unpublished].ShouldBe(1);
        });
    }

    [Fact]
    public async Task Two_published_listings_have_no_beds_free()
    {
        await WithContextAsync(async context =>
        {
            var full = await Seeded(context)
                .CountAsync(p => p.Status == PropertyStatus.Published && p.AvailableBeds == 0);

            full.ShouldBe(2);
        });
    }

    [Fact]
    public async Task Three_listings_carry_the_inspection_badge_and_all_of_them_are_published()
    {
        await WithContextAsync(async context =>
        {
            var badged = await Seeded(context)
                .Where(p => p.HasInspectionBadge)
                .ToListAsync();

            badged.Count.ShouldBe(3);
            badged.ShouldAllBe(p => p.Status == PropertyStatus.Published);
            badged.ShouldAllBe(p => p.LastInspectionAt != null);
        });
    }

    [Fact]
    public async Task Two_published_listings_are_stale_enough_for_the_sweep_to_find()
    {
        await WithContextAsync(async context =>
        {
            var cutoff = DateTime.UtcNow.AddDays(-StaleAfterDays);

            var stale = await Seeded(context)
                .CountAsync(p => p.Status == PropertyStatus.Published
                                 && p.AvailabilityConfirmedAt < cutoff);

            stale.ShouldBe(2);
        });
    }

    /// <summary>
    /// Rule 23 will not let a listing reach Published while the owner's identity is
    /// unchecked, so seed data that breaks it would make the admin phase untestable.
    /// </summary>
    [Fact]
    public async Task Every_published_listing_belongs_to_an_owner_whose_identity_was_checked()
    {
        await WithContextAsync(async context =>
        {
            var unchecked_ = await Seeded(context)
                .Where(p => p.Status == PropertyStatus.Published)
                .Join(context.OwnerProfiles.IgnoreQueryFilters(),
                    p => p.OwnerId, o => o.AppUserId, (p, o) => o.IdentityStatus)
                .Where(status => status != VerificationStatus.Checked)
                .CountAsync();

            unchecked_.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Every_seeded_listing_has_four_photographs_and_exactly_one_primary()
    {
        await WithContextAsync(async context =>
        {
            var perListing = await Seeded(context)
                .Select(p => new
                {
                    Total = p.Photos.Count,
                    Primary = p.Photos.Count(ph => ph.IsPrimary)
                })
                .ToListAsync();

            perListing.Count.ShouldBe(14);
            perListing.ShouldAllBe(x => x.Total == 4);
            perListing.ShouldAllBe(x => x.Primary == 1);
        });
    }

    [Fact]
    public async Task Every_seeded_photograph_is_live_and_positioned()
    {
        await WithContextAsync(async context =>
        {
            var photos = await SeededPhotos(context).ToListAsync();

            photos.Count.ShouldBe(56);
            photos.ShouldAllBe(p => p.Status == PhotoStatus.Live);
            photos.ShouldAllBe(p => p.SortOrder >= 1 && p.SortOrder <= 4);
            photos.ShouldAllBe(p => p.BlobUrl != p.ThumbnailUrl);
        });
    }

    /// <summary>
    /// A seeded URL that resolves to nothing is exactly the fake data the build plan
    /// warns about: it looks right in the database and breaks on the first screen.
    /// </summary>
    [Fact]
    public async Task A_seeded_photograph_really_is_stored_and_really_has_a_thumbnail()
    {
        string? blobUrl = null;
        string? thumbnailUrl = null;

        await WithContextAsync(async context =>
        {
            var photo = await SeededPhotos(context)
                .OrderBy(p => p.Id)
                .FirstAsync();

            blobUrl = photo.BlobUrl;
            thumbnailUrl = photo.ThumbnailUrl;
        });

        var original = await TestImages.MeasureAsync(blobUrl!);
        var thumbnail = await TestImages.MeasureAsync(thumbnailUrl!);

        original.Width.ShouldBe(1000);
        original.Height.ShouldBe(750);
        Math.Max(thumbnail.Width, thumbnail.Height).ShouldBe(400);
    }

    [Fact]
    public async Task Every_seeded_listing_sits_inside_Pakistan()
    {
        await WithContextAsync(async context =>
        {
            var listings = await Seeded(context).ToListAsync();

            foreach (var listing in listings)
            {
                listing.Latitude.ShouldBeInRange(SouthernMost, NorthernMost, listing.Title);
                listing.Longitude.ShouldBeInRange(WesternMost, EasternMost, listing.Title);
            }
        });
    }

    [Fact]
    public async Task No_seeded_listing_claims_more_free_beds_than_it_has()
    {
        await WithContextAsync(async context =>
        {
            var impossible = await Seeded(context)
                .CountAsync(p => p.AvailableBeds > p.TotalBeds || p.AvailableBeds < 0);

            impossible.ShouldBe(0);
        });
    }

    [Fact]
    public async Task Every_seeded_listing_offers_at_least_one_facility()
    {
        await WithContextAsync(async context =>
        {
            var without = await Seeded(context)
                .CountAsync(p => !p.Facilities.Any());

            without.ShouldBe(0);
        });
    }

    /// <summary>
    /// Listings and photographs are written in two saves behind separate guards. A
    /// crash between them once left the database with listings and no photographs,
    /// and a single guard over both would have called that seeded and moved on.
    /// </summary>
    [Fact]
    public async Task Seeding_a_database_that_has_listings_but_no_photographs_fills_the_photographs_in()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();
        var storage = scope.ServiceProvider
            .GetRequiredService<RoomRaah.Application.Interfaces.IPhotoStorageService>();

        await using var transaction = await context.Database.BeginTransactionAsync();

        await SeededPhotos(context).ExecuteDeleteAsync();

        (await SeededPhotos(context).CountAsync()).ShouldBe(0);

        await PropertySeeder.SeedAsync(context, storage);

        (await SeededPhotos(context).CountAsync()).ShouldBe(56);

        await transaction.RollbackAsync();
    }

    /// <summary>
    /// Rule 23 needs four passing checks before anything can be published, so every
    /// published listing carries them or the seed would be describing a state the
    /// product cannot reach.
    /// </summary>
    [Fact]
    public async Task Every_published_listing_carries_the_four_checks_that_let_it_be_published()
    {
        await WithContextAsync(async context =>
        {
            var published = await Seeded(context)
                .Where(p => p.Status == PropertyStatus.Published)
                .Select(p => p.Id)
                .ToListAsync();

            published.ShouldNotBeEmpty();

            foreach (var id in published)
            {
                var passed = await context.VerificationChecks
                    .IgnoreQueryFilters()
                    .Where(c => c.PropertyId == id && c.Status == VerificationStatus.Checked)
                    .Select(c => c.CheckType)
                    .ToListAsync();

                passed.ShouldContain(VerificationCheckType.OwnerIdentity);
                passed.ShouldContain(VerificationCheckType.Address);
                passed.ShouldContain(VerificationCheckType.Photos);
                passed.ShouldContain(VerificationCheckType.Facilities);
            }
        });
    }

    /// <summary>
    /// One of the two waiting for review is deliberately short of a check, so the
    /// refusal rule 23 exists for is visible on a fresh database rather than only in
    /// a test.
    /// </summary>
    [Fact]
    public async Task One_listing_waiting_for_review_is_ready_and_one_is_not()
    {
        await WithContextAsync(async context =>
        {
            var waiting = await Seeded(context)
                .Where(p => p.Status == PropertyStatus.PendingReview)
                .Select(p => p.Id)
                .ToListAsync();

            waiting.Count.ShouldBe(2);

            var passedCounts = new List<int>();

            foreach (var id in waiting)
            {
                passedCounts.Add(await context.VerificationChecks
                    .IgnoreQueryFilters()
                    .CountAsync(c => c.PropertyId == id && c.Status == VerificationStatus.Checked));
            }

            passedCounts.ShouldContain(4);
            passedCounts.ShouldContain(3);
        });
    }

    [Fact]
    public async Task Both_review_queues_have_something_in_them()
    {
        await WithContextAsync(async context =>
        {
            var ids = await Seeded(context).Select(p => p.Id).ToListAsync();

            var pending = await context.PropertyRevisions
                .IgnoreQueryFilters()
                .Where(r => ids.Contains(r.PropertyId) && r.ReviewStatus == RevisionStatus.Pending)
                .ToListAsync();

            pending.ShouldContain(r => r.Kind == RevisionKind.Content);
            pending.ShouldContain(r => r.Kind == RevisionKind.Availability);

            // Every revision belongs to a listing the public can already see, which is
            // the only state in which an owner edit becomes a revision at all.
            foreach (var revision in pending)
            {
                var status = await Seeded(context)
                    .Where(p => p.Id == revision.PropertyId)
                    .Select(p => p.Status)
                    .FirstAsync();

                status.ShouldBe(PropertyStatus.Published);
            }
        });
    }
}
