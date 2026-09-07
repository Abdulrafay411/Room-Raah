using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Search.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.Infrastructure.Services;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

public class SearchPropertiesQueryHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly IFacilityRepository _facilities = Substitute.For<IFacilityRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly SearchPropertiesQueryHandler _handler;

    public SearchPropertiesQueryHandlerTests()
    {
        // The real calculator, not a substitute: the number on the card is the point
        // of several of these tests, and a stubbed distance would prove nothing.
        _handler = new SearchPropertiesQueryHandler(
            _properties, _locations, _facilities, new HaversineDistanceCalculator(),
            _cache, TestMapper.Create());

        _locations.CityExistsAsync(Arg.Any<int>()).Returns(true);
        _locations.AreaExistsAsync(Arg.Any<int>()).Returns(true);
    }

    private static Property Published(int id = 1, double latitude = 31.5204, double longitude = 74.3587)
    {
        var listing = TestData.Property(id, status: PropertyStatus.Published,
            photos: TestData.Photos(3, id));
        listing.Latitude = latitude;
        listing.Longitude = longitude;
        return listing;
    }

    private void Returns(params Property[] listings)
    {
        _properties.SearchAsync(Arg.Any<PropertySearchCriteria>()).Returns(listings.ToList());
        _properties.CountSearchAsync(Arg.Any<PropertySearchCriteria>()).Returns(listings.Length);
    }

    private Task<PagedResult<ListingCardDto>> HandleAsync(SearchPropertiesQuery? query = null) =>
        _handler.Handle(query ?? new SearchPropertiesQuery(), default);

    [Fact]
    public async Task A_search_returns_a_page_of_cards()
    {
        Returns(Published(1), Published(2));

        var page = await HandleAsync();

        page.Items.Count.ShouldBe(2);
        page.TotalCount.ShouldBe(2);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(12);
        page.Items[0].Title.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_filters_reach_the_repository()
    {
        Returns(Published());

        var landmark = TestData.Landmark(3, latitude: 33.6425, longitude: 72.9906);
        _locations.GetLandmarkAsync(3).Returns(landmark);
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1), TestData.Facility(2, "Mess", "mess", 2)]);

        await HandleAsync(new SearchPropertiesQuery
        {
            CityId = 1,
            AreaId = 2,
            LandmarkId = 3,
            MaxDistanceKm = 5,
            MinRent = 10000,
            MaxRent = 25000,
            RoomType = "shared",
            GenderPolicy = "boys",
            FacilityIds = "1,2",
            AvailableOnly = true,
            Sort = "distance",
            Page = 2,
            PageSize = 24
        });

        await _properties.Received(1).SearchAsync(Arg.Is<PropertySearchCriteria>(c =>
            c.CityId == 1 &&
            c.AreaId == 2 &&
            c.LandmarkLatitude == 33.6425 &&
            c.LandmarkLongitude == 72.9906 &&
            c.MaxDistanceKm == 5 &&
            c.MinRent == 10000m &&
            c.MaxRent == 25000m &&
            c.RoomType == RoomType.Shared &&
            c.GenderPolicy == GenderPolicy.Boys &&
            c.AvailableOnly &&
            c.Sort == PropertySort.Distance &&
            c.Page == 2 &&
            c.PageSize == 24));
    }

    [Fact]
    public async Task An_absent_sort_is_the_most_recently_confirmed_first()
    {
        Returns(Published());

        await HandleAsync();

        await _properties.Received(1).SearchAsync(
            Arg.Is<PropertySearchCriteria>(c => c.Sort == PropertySort.Recent));
    }

    [Fact]
    public async Task Rule_80_a_distance_is_measured_from_the_landmark_and_rounded()
    {
        Returns(Published(1, 31.5204, 74.3587));

        // Punjab University to the Gulberg pin: 6.1963 km.
        _locations.GetLandmarkAsync(3)
            .Returns(TestData.Landmark(3, "University of the Punjab", latitude: 31.4966, longitude: 74.2996));

        var page = await HandleAsync(new SearchPropertiesQuery { LandmarkId = 3 });

        page.Items.Single().DistanceKm.ShouldBe(6.2);
    }

    [Fact]
    public async Task Rule_80_no_landmark_means_no_distance_rather_than_a_distance_of_zero()
    {
        Returns(Published());

        var page = await HandleAsync();

        page.Items.Single().DistanceKm.ShouldBeNull();
    }

    [Fact]
    public async Task Rule_83_the_page_is_cached_for_five_minutes_under_its_filters()
    {
        Returns(Published());

        await HandleAsync(new SearchPropertiesQuery { CityId = 1 });

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(key => key.StartsWith(CacheKeys.PropertiesPrefix) && key.Contains("CityId=1")),
            Arg.Any<PagedResult<ListingCardDto>>(),
            CacheKeys.SearchTtl);
    }

    [Fact]
    public async Task A_cached_page_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<PagedResult<ListingCardDto>>(Arg.Any<string>())
            .Returns(new PagedResult<ListingCardDto> { TotalCount = 99 });

        var page = await HandleAsync();

        page.TotalCount.ShouldBe(99);
        await _properties.DidNotReceive().SearchAsync(Arg.Any<PropertySearchCriteria>());
    }

    [Fact]
    public async Task Rule_85_a_city_that_does_not_exist_is_refused()
    {
        _locations.CityExistsAsync(404).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { CityId = 404 }));
    }

    [Fact]
    public async Task Rule_85_an_area_that_does_not_exist_is_refused()
    {
        _locations.AreaExistsAsync(404).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { AreaId = 404 }));
    }

    [Fact]
    public async Task Rule_85_a_landmark_that_does_not_exist_is_refused()
    {
        _locations.GetLandmarkAsync(404).Returns((Landmark?)null);

        await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { LandmarkId = 404 }));
    }

    [Fact]
    public async Task Rule_85_a_facility_that_does_not_exist_is_refused()
    {
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1)]);

        await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { FacilityIds = "1,404" }));
    }

    /// <summary>
    /// Every missing id at once, so a filter panel that has fallen out of date is
    /// fixed in one round trip rather than one id per attempt.
    /// </summary>
    [Fact]
    public async Task Rule_85_names_every_facility_that_is_missing()
    {
        _facilities.GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns([TestData.Facility(1)]);

        var error = await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { FacilityIds = "1,404,405" }));

        error.Message.ShouldContain("404");
        error.Message.ShouldContain("405");
    }

    /// <summary>
    /// The refusal has to arrive on a hit as well as on a miss, so the check runs
    /// before the cache is consulted rather than after it.
    /// </summary>
    [Fact]
    public async Task Rule_85_is_checked_before_the_cache_is_read()
    {
        _locations.CityExistsAsync(404).Returns(false);

        await Should.ThrowAsync<BusinessRuleException>(
            HandleAsync(new SearchPropertiesQuery { CityId = 404 }));

        await _cache.DidNotReceive().GetAsync<PagedResult<ListingCardDto>>(Arg.Any<string>());
    }

    [Fact]
    public async Task No_ids_means_nothing_is_looked_up()
    {
        Returns(Published());

        await HandleAsync();

        await _locations.DidNotReceive().CityExistsAsync(Arg.Any<int>());
        await _locations.DidNotReceive().AreaExistsAsync(Arg.Any<int>());
        await _locations.DidNotReceive().GetLandmarkAsync(Arg.Any<int>());
        await _facilities.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<int>>());
    }
}
