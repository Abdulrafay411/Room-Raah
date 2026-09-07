using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Persistence;

/// <summary>
/// The seeded location data is the backbone of search, the map and the distance
/// filter. These assert against the database the API actually booted on.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReferenceDataSeedTests
{
    // Pakistan, generously bounded. Coordinates outside this are not a rounding
    // slip — they are placeholder data, which is what this test exists to catch.
    private const double SouthernMost = 23.5;
    private const double NorthernMost = 37.1;
    private const double WesternMost = 60.9;
    private const double EasternMost = 77.9;

    private readonly RoomRaahApiFactory _factory;

    public ReferenceDataSeedTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task WithContextAsync(Func<RoomRaahDbContext, Task> assert)
    {
        using var scope = _factory.Services.CreateScope();
        await assert(scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>());
    }

    [Fact]
    public async Task The_counts_match_the_build_plan()
    {
        await WithContextAsync(async context =>
        {
            (await context.Cities.CountAsync()).ShouldBe(3);
            (await context.Areas.CountAsync()).ShouldBe(9);
            (await context.Landmarks.CountAsync()).ShouldBe(8);
            (await context.Facilities.CountAsync()).ShouldBe(7);
        });
    }

    [Fact]
    public async Task Every_city_has_three_areas()
    {
        await WithContextAsync(async context =>
        {
            var perCity = await context.Areas
                .GroupBy(a => a.CityId)
                .Select(g => g.Count())
                .ToListAsync();

            perCity.ShouldAllBe(count => count == 3);
        });
    }

    [Fact]
    public async Task Every_landmark_sits_inside_Pakistan()
    {
        await WithContextAsync(async context =>
        {
            var landmarks = await context.Landmarks.ToListAsync();

            foreach (var landmark in landmarks)
            {
                landmark.Latitude.ShouldBeInRange(SouthernMost, NorthernMost, landmark.Name);
                landmark.Longitude.ShouldBeInRange(WesternMost, EasternMost, landmark.Name);
            }
        });
    }

    [Fact]
    public async Task Landmarks_include_both_universities_and_workplaces()
    {
        await WithContextAsync(async context =>
        {
            var kinds = await context.Landmarks.Select(l => l.Kind).Distinct().ToListAsync();

            kinds.ShouldContain(LandmarkKind.University);
            kinds.ShouldContain(LandmarkKind.Workplace);
        });
    }

    [Fact]
    public async Task No_two_landmarks_share_a_position()
    {
        await WithContextAsync(async context =>
        {
            var landmarks = await context.Landmarks.ToListAsync();

            landmarks
                .Select(l => (l.Latitude, l.Longitude))
                .Distinct()
                .Count()
                .ShouldBe(landmarks.Count);
        });
    }

    [Fact]
    public async Task A_seeded_seeker_carries_a_preferred_city_and_landmark()
    {
        await WithContextAsync(async context =>
        {
            var profile = await context.SeekerProfiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.PreferredCityId != null);

            profile.ShouldNotBeNull();
            profile!.PreferredLandmarkId.ShouldNotBeNull();

            (await context.Cities.AnyAsync(c => c.Id == profile.PreferredCityId)).ShouldBeTrue();
            (await context.Landmarks.AnyAsync(l => l.Id == profile.PreferredLandmarkId)).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task A_preferred_city_that_does_not_exist_is_refused_by_the_database()
    {
        await WithContextAsync(async context =>
        {
            var user = await context.AppUsers
                .IgnoreQueryFilters()
                .FirstAsync(u => u.SeekerProfile == null && u.OwnerProfile == null);

            context.SeekerProfiles.Add(new SeekerProfile
            {
                AppUserId = user.Id,
                PreferredCityId = 999999
            });

            await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task Re_seeding_drops_the_cached_reference_lists()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        // The reference tables are emptied inside a transaction that is rolled back,
        // so the seeder takes the path a fresh database takes and this one is left
        // exactly as it was. Redis is not transactional, which is the point: the
        // eviction is what survives to be asserted.
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // Listings go first. Every foreign key in this product is Restrict, so a
            // listing pointing at an area or a facility is enough to make the deletes
            // below fail — and a half-finished delete inside a test that also writes
            // to a shared cache is how one test breaks every test after it.
            await context.PropertyPhotos.IgnoreQueryFilters().ExecuteDeleteAsync();
            await context.PropertyFacilities.IgnoreQueryFilters().ExecuteDeleteAsync();
            await context.Properties.IgnoreQueryFilters().ExecuteDeleteAsync();

            await context.SeekerProfiles
                .IgnoreQueryFilters()
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.PreferredCityId, (int?)null)
                    .SetProperty(p => p.PreferredLandmarkId, (int?)null));

            await context.Areas.ExecuteDeleteAsync();
            await context.Landmarks.ExecuteDeleteAsync();
            await context.Cities.ExecuteDeleteAsync();
            await context.Facilities.ExecuteDeleteAsync();

            await cache.SetAsync(CacheKeys.Cities(), new List<CityDto>(), TimeSpan.FromHours(1));
            await cache.SetAsync(CacheKeys.LocationSuggest("stale"), new List<LocationSuggestionDto>(), TimeSpan.FromHours(1));
            await cache.SetAsync(CacheKeys.Facilities(), new List<FacilityDto>(), TimeSpan.FromHours(1));

            await ReferenceDataSeeder.SeedAsync(context, cache);

            (await cache.GetAsync<List<CityDto>>(CacheKeys.Cities())).ShouldBeNull();
            (await cache.GetAsync<List<LocationSuggestionDto>>(CacheKeys.LocationSuggest("stale"))).ShouldBeNull();
            (await cache.GetAsync<List<FacilityDto>>(CacheKeys.Facilities())).ShouldBeNull();
        }
        finally
        {
            await transaction.RollbackAsync();

            // The database rolls back; Redis does not. Anything this test wrote has
            // to be cleared by hand, or an empty city list sits in the cache for an
            // hour and every later test reads it instead of the real one.
            await ClearReferenceCacheAsync(cache);
        }
    }

    /// <summary>
    /// Reference lists are cached for a day, so a test that writes a placeholder
    /// under a real key has to take it back out however the test ends.
    /// </summary>
    private static async Task ClearReferenceCacheAsync(ICacheService cache)
    {
        await cache.RemoveByPrefixAsync(CacheKeys.LocationsPrefix);
        await cache.RemoveByPrefixAsync(CacheKeys.FacilitiesPrefix);
    }

    [Fact]
    public async Task Seeding_a_database_that_already_has_reference_data_leaves_the_cache_alone()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        try
        {
            await cache.SetAsync(CacheKeys.Cities(), new List<CityDto>(), TimeSpan.FromHours(1));

            await ReferenceDataSeeder.SeedAsync(context, cache);

            (await cache.GetAsync<List<CityDto>>(CacheKeys.Cities())).ShouldNotBeNull();
        }
        finally
        {
            await ClearReferenceCacheAsync(cache);
        }
    }

    [Fact]
    public async Task Deactivating_a_city_takes_its_areas_out_of_the_lists_with_it()
    {
        await WithContextAsync(async context =>
        {
            var city = await context.Cities.FirstAsync(c => c.Name == "Karachi");

            var visibleBefore = await context.Areas.CountAsync(a => a.CityId == city.Id);
            visibleBefore.ShouldBe(3);

            city.IsActive = false;
            await context.SaveChangesAsync();

            try
            {
                (await context.Areas.CountAsync(a => a.CityId == city.Id)).ShouldBe(0);
                (await context.Landmarks.CountAsync(l => l.CityId == city.Id)).ShouldBe(0);
            }
            finally
            {
                city.IsActive = true;
                await context.SaveChangesAsync();
            }
        });
    }
}
