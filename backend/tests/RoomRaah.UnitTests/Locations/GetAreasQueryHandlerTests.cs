using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Locations.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Locations;

public class GetAreasQueryHandlerTests
{
    private const int CityId = 3;

    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetAreasQueryHandler _handler;

    public GetAreasQueryHandlerTests()
    {
        _handler = new GetAreasQueryHandler(_locations, _cache, TestMapper.Create());
        _locations.CityExistsAsync(CityId).Returns(true);
    }

    private Task<List<AreaDto>> HandleAsync(int cityId = CityId)
    {
        return _handler.Handle(new GetAreasQuery { CityId = cityId }, default);
    }

    [Fact]
    public async Task The_areas_of_the_city_are_returned()
    {
        var city = TestData.City(CityId, "Lahore");
        _locations.GetAreasAsync(CityId).Returns(
            [TestData.Area(1, "Gulberg", city), TestData.Area(2, "Johar Town", city)]);

        var areas = await HandleAsync();

        areas.Select(a => a.Name).ShouldBe(["Gulberg", "Johar Town"]);
        areas.ShouldAllBe(a => a.CityId == CityId);
    }

    [Fact]
    public async Task An_unknown_city_is_not_found()
    {
        _locations.CityExistsAsync(404).Returns(false);

        var error = await Should.ThrowAsync<NotFoundException>(() => HandleAsync(404));

        error.Message.ShouldContain("404");
        await _locations.DidNotReceive().GetAreasAsync(404);
    }

    [Fact]
    public async Task A_city_with_no_areas_yet_is_an_empty_list_rather_than_a_failure()
    {
        _locations.GetAreasAsync(CityId).Returns([]);

        (await HandleAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_list_is_cached_per_city_for_twenty_four_hours()
    {
        _locations.GetAreasAsync(CityId).Returns([TestData.Area()]);

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.Areas(CityId),
            Arg.Any<List<AreaDto>>(),
            CacheKeys.ReferenceDataTtl);
    }

    [Fact]
    public async Task A_cached_list_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<List<AreaDto>>(CacheKeys.Areas(CityId))
            .Returns([new AreaDto { Id = 9, Name = "Cached Area", CityId = CityId }]);

        (await HandleAsync()).Single().Name.ShouldBe("Cached Area");

        await _locations.DidNotReceive().GetAreasAsync(CityId);
        await _locations.DidNotReceive().CityExistsAsync(CityId);
    }

    [Fact]
    public void Two_cities_do_not_share_a_cache_entry()
    {
        CacheKeys.Areas(1).ShouldNotBe(CacheKeys.Areas(2));
    }
}
