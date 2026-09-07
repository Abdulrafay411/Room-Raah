using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Facilities.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Facilities;

public class GetFacilitiesQueryHandlerTests
{
    private readonly IFacilityRepository _facilities = Substitute.For<IFacilityRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetFacilitiesQueryHandler _handler;

    public GetFacilitiesQueryHandlerTests()
    {
        _handler = new GetFacilitiesQueryHandler(_facilities, _cache, TestMapper.Create());
    }

    private Task<List<FacilityDto>> HandleAsync() => _handler.Handle(new GetFacilitiesQuery(), default);

    [Fact]
    public async Task Every_facility_is_returned_with_the_key_the_client_draws_an_icon_from()
    {
        _facilities.GetAllAsync().Returns(
            [TestData.Facility(1, "Wi-Fi", "wifi", 1), TestData.Facility(2, "Mess", "mess", 2)]);

        var facilities = await HandleAsync();

        facilities.Select(f => f.Name).ShouldBe(["Wi-Fi", "Mess"]);
        facilities[0].IconKey.ShouldBe("wifi");
        facilities[0].Id.ShouldBe(1);
    }

    [Fact]
    public async Task The_list_is_cached_for_twenty_four_hours()
    {
        _facilities.GetAllAsync().Returns([TestData.Facility()]);

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.Facilities(),
            Arg.Any<List<FacilityDto>>(),
            CacheKeys.ReferenceDataTtl);
    }

    [Fact]
    public async Task A_cached_list_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<List<FacilityDto>>(CacheKeys.Facilities())
            .Returns([new FacilityDto { Id = 9, Name = "Cached", IconKey = "cached" }]);

        (await HandleAsync()).Single().Name.ShouldBe("Cached");

        await _facilities.DidNotReceive().GetAllAsync();
    }

    [Fact]
    public async Task A_cache_that_is_down_still_answers_from_the_database()
    {
        _cache.GetAsync<List<FacilityDto>>(Arg.Any<string>()).Returns((List<FacilityDto>?)null);
        _facilities.GetAllAsync().Returns([TestData.Facility(1, "Wi-Fi")]);

        (await HandleAsync()).Single().Name.ShouldBe("Wi-Fi");
    }
}
