using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Locations.Queries;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;
using StackExchange.Redis;

namespace RoomRaah.IntegrationTests.Locations;

[Collection(ApiCollection.Name)]
public class LocationEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public LocationEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<List<CityDto>> CitiesAsync()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<List<CityDto>>();
    }

    private async Task<int> CityIdAsync(string name)
    {
        return (await CitiesAsync()).Single(c => c.Name == name).Id;
    }

    private Task<HttpResponseMessage> SuggestAsync(string q)
    {
        return _client.GetAsync($"/api/v1/locations/suggest?q={Uri.EscapeDataString(q)}");
    }

    private async Task<List<LocationSuggestionDto>> SuggestionsAsync(string q)
    {
        var response = await SuggestAsync(q);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<List<LocationSuggestionDto>>();
    }

    private async Task<bool> CachedAsync(string key)
    {
        var redis = _factory.Services.GetRequiredService<IConnectionMultiplexer>();
        return await redis.GetDatabase().KeyExistsAsync(key);
    }

    // --- Cities ---

    [Fact]
    public async Task The_seeded_cities_are_returned_alphabetically()
    {
        var cities = await CitiesAsync();

        cities.Select(c => c.Name).ShouldBe(["Islamabad", "Karachi", "Lahore"]);
        cities.ShouldAllBe(c => c.Id > 0);
    }

    [Fact]
    public async Task A_visitor_with_no_token_can_read_the_cities()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/locations/cities");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reading_the_cities_fills_the_cache()
    {
        await CitiesAsync();

        (await CachedAsync(CacheKeys.Cities())).ShouldBeTrue();
    }

    [Fact]
    public async Task A_second_read_of_the_cities_returns_the_same_list()
    {
        var first = await CitiesAsync();
        var second = await CitiesAsync();

        second.Select(c => c.Id).ShouldBe(first.Select(c => c.Id));
        second.Select(c => c.Name).ShouldBe(first.Select(c => c.Name));
    }

    // --- Areas ---

    [Fact]
    public async Task The_areas_of_a_city_are_returned_alphabetically()
    {
        var lahoreId = await CityIdAsync("Lahore");

        var response = await _client.GetAsync($"/api/v1/locations/cities/{lahoreId}/areas");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var areas = await response.ReadAsync<List<AreaDto>>();
        areas.Select(a => a.Name).ShouldBe(["Gulberg", "Johar Town", "Model Town"]);
        areas.ShouldAllBe(a => a.CityId == lahoreId);
    }

    [Fact]
    public async Task Each_city_has_its_own_areas()
    {
        var lahore = await CityIdAsync("Lahore");
        var karachi = await CityIdAsync("Karachi");

        var lahoreAreas = await (await _client.GetAsync($"/api/v1/locations/cities/{lahore}/areas"))
            .ReadAsync<List<AreaDto>>();
        var karachiAreas = await (await _client.GetAsync($"/api/v1/locations/cities/{karachi}/areas"))
            .ReadAsync<List<AreaDto>>();

        lahoreAreas.Select(a => a.Name).ShouldNotBe(karachiAreas.Select(a => a.Name));
        karachiAreas.Select(a => a.Name).ShouldBe(["Clifton", "Gulshan-e-Iqbal", "North Nazimabad"]);
    }

    [Fact]
    public async Task An_unknown_city_is_404()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities/999999/areas");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_city_id_of_zero_is_400()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities/0/areas");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "CityId");
    }

    [Fact]
    public async Task Reading_the_areas_fills_a_cache_entry_of_its_own_per_city()
    {
        var islamabadId = await CityIdAsync("Islamabad");

        await _client.GetAsync($"/api/v1/locations/cities/{islamabadId}/areas");

        (await CachedAsync(CacheKeys.Areas(islamabadId))).ShouldBeTrue();
    }

    // --- Suggestions ---

    [Fact]
    public async Task Three_letters_return_real_suggestions()
    {
        var suggestions = await SuggestionsAsync("lah");

        suggestions.ShouldNotBeEmpty();
        suggestions.ShouldContain(s => s.Name == "Lahore" && s.Type == LocationSuggestionDto.CityType);
    }

    [Fact]
    public async Task A_word_shared_by_a_city_and_a_landmark_returns_both_in_the_documented_order()
    {
        // The seeded set has no case where a non-city begins with a term that a city
        // merely contains, so this pins the end-to-end order for a real query and the
        // prefix rule itself is isolated in SuggestLocationsQueryHandlerTests.
        var suggestions = await SuggestionsAsync("lahore");

        suggestions.Select(s => s.Name).ShouldBe(["Lahore", "UET Lahore"]);
        suggestions.First().Type.ShouldBe(LocationSuggestionDto.CityType);
        suggestions.Last().Type.ShouldBe(LocationSuggestionDto.LandmarkType);
    }

    [Fact]
    public async Task A_landmark_suggestion_carries_the_coordinates_the_distance_filter_needs()
    {
        var nust = (await SuggestionsAsync("nust")).Single();

        nust.Type.ShouldBe(LocationSuggestionDto.LandmarkType);
        nust.Kind.ShouldBe("University");
        nust.City.ShouldBe("Islamabad");
        nust.Latitude!.Value.ShouldBe(33.64, tolerance: 0.05);
        nust.Longitude!.Value.ShouldBe(72.99, tolerance: 0.05);
    }

    [Fact]
    public async Task A_workplace_landmark_is_labelled_as_one()
    {
        var blueArea = (await SuggestionsAsync("blue area")).Single();

        blueArea.Kind.ShouldBe("Workplace");
    }

    [Fact]
    public async Task A_city_suggestion_carries_no_coordinates_and_no_parent_city()
    {
        var karachi = (await SuggestionsAsync("karachi"))
            .First(s => s.Type == LocationSuggestionDto.CityType);

        karachi.Latitude.ShouldBeNull();
        karachi.Longitude.ShouldBeNull();
        karachi.Kind.ShouldBeNull();
        karachi.City.ShouldBeNull();
    }

    [Fact]
    public async Task Two_areas_with_similar_names_are_told_apart_by_their_city()
    {
        var suggestions = await SuggestionsAsync("gul");

        var areas = suggestions.Where(s => s.Type == LocationSuggestionDto.AreaType).ToList();

        areas.ShouldContain(a => a.Name == "Gulberg" && a.City == "Lahore");
        areas.ShouldContain(a => a.Name == "Gulshan-e-Iqbal" && a.City == "Karachi");
    }

    [Fact]
    public async Task Matching_ignores_case()
    {
        var lower = await SuggestionsAsync("nust");
        var upper = await SuggestionsAsync("NUST");

        upper.Select(s => s.Id).ShouldBe(lower.Select(s => s.Id));
    }

    [Fact]
    public async Task No_more_than_ten_suggestions_come_back()
    {
        (await SuggestionsAsync("an")).Count
            .ShouldBeLessThanOrEqualTo(SuggestLocationsQuery.MaxSuggestions);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task A_term_shorter_than_two_characters_is_400(string q)
    {
        var response = await SuggestAsync(q);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "q");
    }

    [Fact]
    public async Task A_missing_term_is_400()
    {
        var response = await _client.GetAsync("/api/v1/locations/suggest");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_term_longer_than_sixty_characters_is_400()
    {
        var response = await SuggestAsync(new string('a', 61));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_term_matching_nothing_is_an_empty_list_not_an_error()
    {
        var response = await SuggestAsync("zzqq");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<List<LocationSuggestionDto>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Suggestions_are_cached_under_the_normalised_term()
    {
        await SuggestionsAsync("  LUms  ");

        (await CachedAsync(CacheKeys.LocationSuggest("lums"))).ShouldBeTrue();
    }

    [Fact]
    public async Task A_visitor_with_no_token_can_ask_for_suggestions()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/locations/suggest?q=nust");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
