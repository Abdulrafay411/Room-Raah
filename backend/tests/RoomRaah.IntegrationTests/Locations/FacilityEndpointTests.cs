using System.Net;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;
using StackExchange.Redis;

namespace RoomRaah.IntegrationTests.Locations;

[Collection(ApiCollection.Name)]
public class FacilityEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public FacilityEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<List<FacilityDto>> FacilitiesAsync()
    {
        var response = await _client.GetAsync("/api/v1/facilities");
        response.EnsureSuccessStatusCode();
        return await response.ReadAsync<List<FacilityDto>>();
    }

    [Fact]
    public async Task The_seven_facilities_from_the_product_doc_are_returned_in_display_order()
    {
        var facilities = await FacilitiesAsync();

        facilities.Select(f => f.Name).ShouldBe(
        [
            "Wi-Fi",
            "Mess",
            "Air Conditioning",
            "Laundry",
            "Parking",
            "Security",
            "Electricity Backup"
        ]);
    }

    [Fact]
    public async Task Every_facility_carries_an_icon_key_and_an_id()
    {
        var facilities = await FacilitiesAsync();

        facilities.ShouldAllBe(f => f.Id > 0);
        facilities.ShouldAllBe(f => f.IconKey.Length > 0);
        facilities.Single(f => f.Name == "Wi-Fi").IconKey.ShouldBe("wifi");
    }

    [Fact]
    public async Task A_visitor_with_no_token_can_read_the_facilities()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/facilities");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reading_the_facilities_fills_the_cache()
    {
        await FacilitiesAsync();

        var redis = _factory.Services.GetRequiredService<IConnectionMultiplexer>();

        (await redis.GetDatabase().KeyExistsAsync(CacheKeys.Facilities())).ShouldBeTrue();
    }

    [Fact]
    public async Task A_second_read_returns_the_same_list()
    {
        var first = await FacilitiesAsync();
        var second = await FacilitiesAsync();

        second.Select(f => f.Id).ShouldBe(first.Select(f => f.Id));
    }
}
