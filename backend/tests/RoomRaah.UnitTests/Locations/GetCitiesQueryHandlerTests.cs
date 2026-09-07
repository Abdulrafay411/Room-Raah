using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Locations.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Locations;

public class GetCitiesQueryHandlerTests
{
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetCitiesQueryHandler _handler;

    public GetCitiesQueryHandlerTests()
    {
        _handler = new GetCitiesQueryHandler(_locations, _cache, TestMapper.Create());
    }

    private Task<List<CityDto>> HandleAsync() => _handler.Handle(new GetCitiesQuery(), default);

    [Fact]
    public async Task Every_city_is_returned()
    {
        _locations.GetCitiesAsync().Returns([TestData.City(1, "Lahore"), TestData.City(2, "Karachi")]);

        var cities = await HandleAsync();

        cities.Count.ShouldBe(2);
        cities.Select(c => c.Name).ShouldBe(["Lahore", "Karachi"]);
        cities[0].Id.ShouldBe(1);
    }

    [Fact]
    public async Task The_list_is_cached_for_twenty_four_hours()
    {
        _locations.GetCitiesAsync().Returns([TestData.City()]);

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.Cities(),
            Arg.Any<List<CityDto>>(),
            CacheKeys.ReferenceDataTtl);
    }

    [Fact]
    public async Task A_cached_list_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<List<CityDto>>(CacheKeys.Cities())
            .Returns([new CityDto { Id = 9, Name = "Cached City" }]);

        var cities = await HandleAsync();

        cities.Single().Name.ShouldBe("Cached City");
        await _locations.DidNotReceive().GetCitiesAsync();
    }

    [Fact]
    public async Task A_cache_that_is_down_still_answers_from_the_database()
    {
        _cache.GetAsync<List<CityDto>>(Arg.Any<string>()).Returns((List<CityDto>?)null);
        _locations.GetCitiesAsync().Returns([TestData.City(1, "Lahore")]);

        (await HandleAsync()).Single().Name.ShouldBe("Lahore");
    }
}
