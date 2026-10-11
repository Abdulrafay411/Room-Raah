using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Search.Queries;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

public class SearchPropertiesQueryValidatorTests
{
    private readonly SearchPropertiesQueryValidator _validator = new();

    [Fact]
    public void An_empty_search_is_a_valid_search()
    {
        _validator.TestValidate(new SearchPropertiesQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Every_filter_together_is_still_valid()
    {
        var query = new SearchPropertiesQuery
        {
            CityId = 1,
            AreaId = 2,
            LandmarkId = 3,
            MaxDistanceKm = 5,
            MinRent = 10000,
            MaxRent = 25000,
            RoomType = "Shared",
            GenderPolicy = "Boys",
            FacilityIds = "1,3",
            AvailableOnly = true,
            MinLatitude = 31.40,
            MaxLatitude = 31.60,
            MinLongitude = 74.20,
            MaxLongitude = 74.40,
            Sort = "Distance",
            Page = 2,
            PageSize = 24
        };

        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rule_30_page_size_may_not_exceed_fifty()
    {
        _validator.TestValidate(new SearchPropertiesQuery { PageSize = 51 })
            .ShouldHaveValidationErrorFor("pageSize");
    }

    [Fact]
    public void Rule_30_the_default_page_size_is_twelve()
    {
        new SearchPropertiesQuery().PageSize.ShouldBe(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_page_number_below_one_is_refused(int page)
    {
        _validator.TestValidate(new SearchPropertiesQuery { Page = page })
            .ShouldHaveValidationErrorFor("page");
    }

    [Fact]
    public void Rule_74_a_maximum_distance_without_a_landmark_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MaxDistanceKm = 5 })
            .ShouldHaveValidationErrorFor("maxDistanceKm");
    }

    [Fact]
    public void Rule_74_a_maximum_distance_with_a_landmark_is_accepted()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MaxDistanceKm = 5, LandmarkId = 1 })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rule_74_sorting_by_distance_without_a_landmark_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { Sort = "Distance" })
            .ShouldHaveValidationErrorFor("sort");
    }

    [Fact]
    public void Rule_74_sorting_by_price_without_a_landmark_is_fine()
    {
        _validator.TestValidate(new SearchPropertiesQuery { Sort = "PriceLowToHigh" })
            .ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// The rule reads the sort as text, and a client that shouts is still a client
    /// asking to sort by distance.
    /// </summary>
    [Fact]
    public void Rule_74_holds_whatever_case_the_sort_arrives_in()
    {
        _validator.TestValidate(new SearchPropertiesQuery { Sort = "distance" })
            .ShouldHaveValidationErrorFor("sort");
    }

    [Fact]
    public void Rule_75_a_minimum_above_the_maximum_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MinRent = 30000, MaxRent = 20000 })
            .ShouldHaveValidationErrorFor("minRent");
    }

    [Fact]
    public void Rule_75_an_equal_minimum_and_maximum_is_a_search_for_one_price()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MinRent = 20000, MaxRent = 20000 })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rule_75_a_minimum_on_its_own_is_not_compared_with_anything()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MinRent = 30000 })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_negative_rent_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MinRent = -1 })
            .ShouldHaveValidationErrorFor("minRent");
    }

    [Fact]
    public void A_room_type_the_product_does_not_have_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { RoomType = "Twin" })
            .ShouldHaveValidationErrorFor("roomType");
    }

    /// <summary>Absent is not the same as wrong: an empty filter is no filter.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_empty_room_type_is_no_filter_rather_than_a_bad_one(string? roomType)
    {
        _validator.TestValidate(new SearchPropertiesQuery { RoomType = roomType })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("private")]
    [InlineData("Shared")]
    public void A_room_type_is_read_case_insensitively(string roomType)
    {
        _validator.TestValidate(new SearchPropertiesQuery { RoomType = roomType })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_gender_policy_the_product_does_not_have_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { GenderPolicy = "Mixed" })
            .ShouldHaveValidationErrorFor("genderPolicy");
    }

    [Fact]
    public void A_sort_the_product_does_not_have_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { Sort = "Cheapest" })
            .ShouldHaveValidationErrorFor("sort");
    }

    [Theory]
    [InlineData("1,two")]
    [InlineData("abc")]
    public void Facility_ids_that_are_not_numbers_are_refused(string ids)
    {
        _validator.TestValidate(new SearchPropertiesQuery { FacilityIds = ids })
            .ShouldHaveValidationErrorFor("facilityIds");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(101.0)]
    public void A_distance_outside_the_documented_range_is_refused(double kilometres)
    {
        _validator.TestValidate(
                new SearchPropertiesQuery { LandmarkId = 1, MaxDistanceKm = kilometres })
            .ShouldHaveValidationErrorFor("maxDistanceKm");
    }

    private static SearchPropertiesQuery Box() => new()
    {
        MinLatitude = 31.40,
        MaxLatitude = 31.60,
        MinLongitude = 74.20,
        MaxLongitude = 74.40
    };

    [Fact]
    public void Rule_133_a_whole_map_box_is_accepted_without_a_landmark()
    {
        _validator.TestValidate(Box()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rule_133_a_map_box_missing_an_edge_is_refused()
    {
        var query = Box();
        query.MaxLongitude = null;

        _validator.TestValidate(query).ShouldHaveValidationErrorFor("minLatitude");
    }

    [Fact]
    public void Rule_133_a_single_edge_on_its_own_is_refused()
    {
        _validator.TestValidate(new SearchPropertiesQuery { MaxLatitude = 31.60 })
            .ShouldHaveValidationErrorFor("minLatitude");
    }

    [Fact]
    public void Rule_133_a_latitude_off_the_globe_is_refused()
    {
        var query = Box();
        query.MaxLatitude = 91;

        _validator.TestValidate(query).ShouldHaveValidationErrorFor("maxLatitude");
    }

    [Fact]
    public void Rule_133_a_longitude_off_the_globe_is_refused()
    {
        var query = Box();
        query.MinLongitude = -181;

        _validator.TestValidate(query).ShouldHaveValidationErrorFor("minLongitude");
    }

    [Fact]
    public void Rule_133_a_box_with_its_edges_swapped_is_refused()
    {
        var latitudes = Box();
        (latitudes.MinLatitude, latitudes.MaxLatitude) = (latitudes.MaxLatitude, latitudes.MinLatitude);
        _validator.TestValidate(latitudes).ShouldHaveValidationErrorFor("minLatitude");

        var longitudes = Box();
        (longitudes.MinLongitude, longitudes.MaxLongitude) = (longitudes.MaxLongitude, longitudes.MinLongitude);
        _validator.TestValidate(longitudes).ShouldHaveValidationErrorFor("minLongitude");
    }
}

public class ComparePropertiesQueryValidatorTests
{
    private readonly ComparePropertiesQueryValidator _validator = new();

    [Fact]
    public void Three_ids_are_accepted()
    {
        _validator.TestValidate(new ComparePropertiesQuery { Ids = "1,2,3" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ids_are_required(string? ids)
    {
        _validator.TestValidate(new ComparePropertiesQuery { Ids = ids })
            .ShouldHaveValidationErrorFor("ids");
    }

    [Fact]
    public void Ids_that_are_not_numbers_are_refused()
    {
        _validator.TestValidate(new ComparePropertiesQuery { Ids = "1,,x" })
            .ShouldHaveValidationErrorFor("ids");
    }

    /// <summary>
    /// Four ids pass the validator and are refused by the handler: rule 35 answers
    /// 422, and a request that is well formed but asks for too much is not a 400.
    /// </summary>
    [Fact]
    public void The_number_of_ids_is_not_decided_here()
    {
        _validator.TestValidate(new ComparePropertiesQuery { Ids = "1,2,3,4" })
            .ShouldNotHaveAnyValidationErrors();
    }
}
