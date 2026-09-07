using RoomRaah.Application.Caching;
using Shouldly;

namespace RoomRaah.UnitTests.Caching;

/// <summary>
/// The handler tests assert that a handler writes with <c>ReferenceDataTtl</c>, which
/// says nothing about what that lifetime actually is — both sides read the same
/// constant. These tests pin the values themselves against the documented ones.
/// </summary>
public class CacheKeysTests
{
    [Fact]
    public void Reference_data_lives_for_twenty_four_hours()
    {
        CacheKeys.ReferenceDataTtl.ShouldBe(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Every_location_key_is_dropped_by_the_prefix_the_seeder_evicts()
    {
        string[] keys =
        [
            CacheKeys.Cities(),
            CacheKeys.Areas(1),
            CacheKeys.LocationSuggest("nust")
        ];

        keys.ShouldAllBe(key => key.StartsWith(CacheKeys.LocationsPrefix));
    }

    [Fact]
    public void The_facility_key_is_dropped_by_the_prefix_the_seeder_evicts()
    {
        CacheKeys.Facilities().ShouldStartWith(CacheKeys.FacilitiesPrefix);
    }

    [Fact]
    public void Location_keys_and_facility_keys_do_not_evict_each_other()
    {
        CacheKeys.Facilities().ShouldNotStartWith(CacheKeys.LocationsPrefix);
        CacheKeys.Cities().ShouldNotStartWith(CacheKeys.FacilitiesPrefix);
    }

    [Fact]
    public void Each_city_has_its_own_area_key()
    {
        CacheKeys.Areas(1).ShouldNotBe(CacheKeys.Areas(2));
    }

    [Fact]
    public void Each_term_has_its_own_suggestion_key()
    {
        CacheKeys.LocationSuggest("nust").ShouldNotBe(CacheKeys.LocationSuggest("lums"));
    }

    [Fact]
    public void The_documented_key_names_are_the_ones_that_are_built()
    {
        CacheKeys.Cities().ShouldBe("locations:cities");
        CacheKeys.Areas(7).ShouldBe("locations:areas:7");
        CacheKeys.LocationSuggest("nust").ShouldBe("locations:suggest:nust");
        CacheKeys.Facilities().ShouldBe("facilities:all");
    }
}
