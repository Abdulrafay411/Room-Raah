using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Locations.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Locations;

public class SuggestLocationsQueryHandlerTests
{
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly SuggestLocationsQueryHandler _handler;

    public SuggestLocationsQueryHandlerTests()
    {
        _handler = new SuggestLocationsQueryHandler(_locations, _cache, TestMapper.Create());

        _locations.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<int>()).Returns([]);
        _locations.SearchAreasAsync(Arg.Any<string>(), Arg.Any<int>()).Returns([]);
        _locations.SearchLandmarksAsync(Arg.Any<string>(), Arg.Any<int>()).Returns([]);
    }

    private void Cities(params City[] cities) =>
        _locations.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(cities.ToList());

    private void Areas(params Area[] areas) =>
        _locations.SearchAreasAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(areas.ToList());

    private void Landmarks(params Landmark[] landmarks) =>
        _locations.SearchLandmarksAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(landmarks.ToList());

    private Task<List<LocationSuggestionDto>> HandleAsync(string q)
    {
        return _handler.Handle(new SuggestLocationsQuery { Q = q }, default);
    }

    [Fact]
    public async Task Cities_areas_and_landmarks_all_appear_in_one_list()
    {
        Cities(TestData.City(1, "Lahore"));
        Areas(TestData.Area(2, "Lahore Cantt"));
        Landmarks(TestData.Landmark(3, "Lahore Business Park", LandmarkKind.Workplace));

        var suggestions = await HandleAsync("lahore");

        suggestions.Select(s => s.Type).ShouldBe(
        [
            LocationSuggestionDto.CityType,
            LocationSuggestionDto.AreaType,
            LocationSuggestionDto.LandmarkType
        ]);
    }

    [Fact]
    public async Task A_name_that_starts_with_the_term_outranks_one_that_merely_contains_it()
    {
        Landmarks(
            TestData.Landmark(1, "Bahria University"),
            TestData.Landmark(2, "NUST"));

        var suggestions = await HandleAsync("nu");

        suggestions.First().Name.ShouldBe("NUST");
    }

    [Fact]
    public async Task A_prefix_matching_area_outranks_a_city_that_only_contains_the_term()
    {
        // "Islamabad" contains "la"; "Lahore Cantt" begins with it.
        Cities(TestData.City(1, "Islamabad"));
        Areas(TestData.Area(2, "Lahore Cantt"));

        var suggestions = await HandleAsync("la");

        suggestions.First().Name.ShouldBe("Lahore Cantt");
        suggestions.Last().Name.ShouldBe("Islamabad");
    }

    [Fact]
    public async Task Among_equally_good_matches_cities_come_before_areas_and_areas_before_landmarks()
    {
        // One name, three kinds of place: nothing but the type can order these.
        Cities(TestData.City(1, "Central"));
        Areas(TestData.Area(2, "Central"));
        Landmarks(TestData.Landmark(3, "Central"));

        var suggestions = await HandleAsync("central");

        suggestions.Select(s => s.Type).ShouldBe(
        [
            LocationSuggestionDto.CityType,
            LocationSuggestionDto.AreaType,
            LocationSuggestionDto.LandmarkType
        ]);
    }

    [Fact]
    public async Task Matching_is_case_insensitive_when_ranking()
    {
        Landmarks(TestData.Landmark(1, "NUST"));

        var suggestions = await HandleAsync("NU");

        suggestions.Single().Name.ShouldBe("NUST");
    }

    [Fact]
    public async Task No_more_than_ten_suggestions_are_returned()
    {
        Areas(Enumerable.Range(1, 30)
            .Select(i => TestData.Area(i, $"Area {i:D2}"))
            .ToArray());

        (await HandleAsync("area")).Count.ShouldBe(SuggestLocationsQuery.MaxSuggestions);
    }

    [Fact]
    public async Task Each_source_is_asked_for_no_more_than_the_final_limit()
    {
        await HandleAsync("nu");

        await _locations.Received(1).SearchCitiesAsync("nu", SuggestLocationsQuery.MaxSuggestions);
        await _locations.Received(1).SearchAreasAsync("nu", SuggestLocationsQuery.MaxSuggestions);
        await _locations.Received(1).SearchLandmarksAsync("nu", SuggestLocationsQuery.MaxSuggestions);
    }

    [Fact]
    public async Task A_landmark_carries_its_coordinates_kind_and_city()
    {
        Landmarks(TestData.Landmark(
            7, "NUST", LandmarkKind.University, 33.6425, 72.9906, TestData.City(2, "Islamabad")));

        var landmark = (await HandleAsync("nu")).Single();

        landmark.Id.ShouldBe(7);
        landmark.Latitude.ShouldBe(33.6425);
        landmark.Longitude.ShouldBe(72.9906);
        landmark.Kind.ShouldBe("University");
        landmark.City.ShouldBe("Islamabad");
    }

    [Fact]
    public async Task An_area_carries_the_city_it_sits_in_so_two_Gulbergs_can_be_told_apart()
    {
        Areas(
            TestData.Area(1, "Gulberg", TestData.City(1, "Lahore")),
            TestData.Area(2, "Gulberg", TestData.City(2, "Islamabad")));

        var suggestions = await HandleAsync("gul");

        suggestions.Select(s => s.City).ShouldBe(["Lahore", "Islamabad"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_city_and_an_area_carry_no_coordinates_because_only_landmarks_anchor_distance()
    {
        Cities(TestData.City(1, "Lahore"));
        Areas(TestData.Area(2, "Lahore Cantt"));

        foreach (var suggestion in await HandleAsync("lahore"))
        {
            suggestion.Latitude.ShouldBeNull();
            suggestion.Longitude.ShouldBeNull();
            suggestion.Kind.ShouldBeNull();
        }
    }

    [Fact]
    public async Task A_city_row_names_no_parent_city_because_it_is_the_city()
    {
        Cities(TestData.City(1, "Lahore"));

        (await HandleAsync("lah")).Single().City.ShouldBeNull();
    }

    [Fact]
    public async Task The_term_is_trimmed_and_lower_cased_before_it_reaches_the_database()
    {
        await HandleAsync("  NUst  ");

        await _locations.Received(1).SearchCitiesAsync("nust", Arg.Any<int>());
    }

    [Fact]
    public async Task Queries_differing_only_by_case_or_padding_share_one_cache_entry()
    {
        await HandleAsync("  NUst ");

        await _cache.Received(1).SetAsync(
            CacheKeys.LocationSuggest("nust"),
            Arg.Any<List<LocationSuggestionDto>>(),
            CacheKeys.ReferenceDataTtl);
    }

    [Fact]
    public async Task A_cached_list_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<List<LocationSuggestionDto>>(CacheKeys.LocationSuggest("nust"))
            .Returns([new LocationSuggestionDto { Name = "Cached", Type = LocationSuggestionDto.CityType }]);

        (await HandleAsync("nust")).Single().Name.ShouldBe("Cached");

        await _locations.DidNotReceive().SearchCitiesAsync(Arg.Any<string>(), Arg.Any<int>());
    }

    [Fact]
    public async Task A_term_that_matches_nothing_is_an_empty_list_rather_than_a_failure()
    {
        (await HandleAsync("zzz")).ShouldBeEmpty();
    }
}
