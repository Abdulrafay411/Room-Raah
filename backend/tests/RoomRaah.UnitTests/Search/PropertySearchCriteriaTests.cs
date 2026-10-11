using System.Reflection;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Enums;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

/// <summary>
/// Rule 83, and the quietest way this phase could go wrong: a filter the query uses
/// and the cache key does not know about serves one search's results to another.
/// </summary>
public class PropertySearchCriteriaTests
{
    private static PropertySearchCriteria Full() => new()
    {
        CityId = 1,
        AreaId = 2,
        LandmarkLatitude = 33.6425,
        LandmarkLongitude = 72.9906,
        MaxDistanceKm = 5,
        MinRent = 10000m,
        MaxRent = 25000m,
        RoomType = RoomType.Shared,
        GenderPolicy = GenderPolicy.Boys,
        FacilityIds = [3, 1],
        AvailableOnly = true,
        MinLatitude = 31.40,
        MaxLatitude = 31.60,
        MinLongitude = 74.20,
        MaxLongitude = 74.40,
        Sort = PropertySort.Distance,
        Page = 2,
        PageSize = 24
    };

    /// <summary>
    /// The one that catches the next person, including me. Walking the properties by
    /// reflection means a filter added later is covered without anyone remembering
    /// to come back here.
    /// </summary>
    [Fact]
    public void Every_filter_on_the_criteria_appears_in_the_key()
    {
        var description = Full().Describe();

        var missing = typeof(PropertySearchCriteria)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(name => !description.Contains(name, StringComparison.Ordinal))
            .ToList();

        missing.ShouldBeEmpty(
            $"These filters are not in the cache key: {string.Join(", ", missing)}. " +
            "A search that differs only by one of them would be served the other one's page.");
    }

    [Fact]
    public void Two_identical_searches_share_one_entry()
    {
        Full().Describe().ShouldBe(Full().Describe());
    }

    [Fact]
    public void The_order_facilities_were_ticked_in_does_not_make_a_second_entry()
    {
        var first = new PropertySearchCriteria { FacilityIds = [3, 1, 2] };
        var second = new PropertySearchCriteria { FacilityIds = [1, 2, 3] };

        first.Describe().ShouldBe(second.Describe());
    }

    [Fact]
    public void A_facility_ticked_twice_does_not_make_a_second_entry()
    {
        var once = new PropertySearchCriteria { FacilityIds = [1, 2] };
        var twice = new PropertySearchCriteria { FacilityIds = [1, 2, 2] };

        once.Describe().ShouldBe(twice.Describe());
    }

    [Theory]
    [InlineData(1, 2)]
    public void Two_different_searches_do_not_share_an_entry(int firstCity, int secondCity)
    {
        new PropertySearchCriteria { CityId = firstCity }.Describe()
            .ShouldNotBe(new PropertySearchCriteria { CityId = secondCity }.Describe());
    }

    [Fact]
    public void A_filter_that_was_given_is_not_the_same_as_one_that_was_not()
    {
        new PropertySearchCriteria().Describe()
            .ShouldNotBe(new PropertySearchCriteria { AvailableOnly = true }.Describe());
    }

    [Fact]
    public void Rule_134_two_map_boxes_do_not_share_an_entry()
    {
        var lahore = new PropertySearchCriteria
        {
            MinLatitude = 31.40, MaxLatitude = 31.60, MinLongitude = 74.20, MaxLongitude = 74.40
        };
        var karachi = new PropertySearchCriteria
        {
            MinLatitude = 24.80, MaxLatitude = 25.00, MinLongitude = 67.00, MaxLongitude = 67.20
        };

        lahore.Describe().ShouldNotBe(karachi.Describe());
        lahore.Describe().ShouldNotBe(new PropertySearchCriteria().Describe());
    }

    [Fact]
    public void Page_two_is_not_page_one()
    {
        new PropertySearchCriteria { Page = 1 }.Describe()
            .ShouldNotBe(new PropertySearchCriteria { Page = 2 }.Describe());
    }

    /// <summary>
    /// A decimal and a double both render differently under a Turkish or a German
    /// culture, and a key that changes with the server's locale is a key that misses
    /// on a machine that formats numbers another way.
    /// </summary>
    [Fact]
    public void The_key_does_not_change_with_the_servers_culture()
    {
        var criteria = new PropertySearchCriteria { MinRent = 10500.5m, LandmarkLatitude = 33.6425 };
        var expected = criteria.Describe();

        var original = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            criteria.Describe().ShouldBe(expected);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void Rule_71_every_public_listing_key_is_dropped_by_the_prefix_a_decision_evicts()
    {
        string[] keys =
        [
            CacheKeys.PropertySearch(Full().Describe()),
            CacheKeys.PropertyDetail(1),
            CacheKeys.PropertyPhotos(1),
            CacheKeys.Featured()
        ];

        keys.ShouldAllBe(key => key.StartsWith(CacheKeys.PropertiesPrefix));
    }

    [Fact]
    public void The_documented_lifetimes_are_the_ones_that_are_used()
    {
        CacheKeys.SearchTtl.ShouldBe(TimeSpan.FromMinutes(5));
        CacheKeys.DetailTtl.ShouldBe(TimeSpan.FromMinutes(15));
        CacheKeys.FeaturedTtl.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Each_listing_has_its_own_detail_key()
    {
        CacheKeys.PropertyDetail(1).ShouldNotBe(CacheKeys.PropertyDetail(2));
        CacheKeys.PropertyDetail(1).ShouldNotBe(CacheKeys.PropertyPhotos(1));
    }
}
