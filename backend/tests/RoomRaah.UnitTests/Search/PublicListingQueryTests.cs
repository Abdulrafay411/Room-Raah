using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Search.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.Infrastructure.Services;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Search;

public class GetPropertyQueryHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetPropertyQueryHandler _handler;

    public GetPropertyQueryHandlerTests()
    {
        _handler = new GetPropertyQueryHandler(_properties, _cache, TestMapper.Create());
    }

    private Task<ListingDetailDto> HandleAsync(int id = 1) =>
        _handler.Handle(new GetPropertyQuery { PropertyId = id }, default);

    [Fact]
    public async Task A_published_listing_comes_back_in_full()
    {
        _properties.GetPublishedAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));

        var detail = await HandleAsync();

        detail.Id.ShouldBe(1);
        detail.Description.ShouldNotBeNullOrWhiteSpace();
        detail.AddressLine.ShouldNotBeNullOrWhiteSpace();
        detail.Photos.Count.ShouldBe(3);
        detail.Checks.ShouldNotBeEmpty();
        detail.Owner.DisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rule_28_a_listing_that_is_not_published_is_not_found()
    {
        _properties.GetPublishedAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync());
    }

    /// <summary>
    /// A listing an admin is minutes away from approving would otherwise be held
    /// back for a quarter of an hour after they did.
    /// </summary>
    [Fact]
    public async Task A_miss_is_not_cached()
    {
        _properties.GetPublishedAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync());

        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<ListingDetailDto>(), Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task The_detail_is_cached_for_fifteen_minutes()
    {
        _properties.GetPublishedAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.PropertyDetail(1), Arg.Any<ListingDetailDto>(), CacheKeys.DetailTtl);
    }

    [Fact]
    public async Task A_cached_detail_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<ListingDetailDto>(CacheKeys.PropertyDetail(1))
            .Returns(new ListingDetailDto { Id = 1, Title = "Cached" });

        (await HandleAsync()).Title.ShouldBe("Cached");

        await _properties.DidNotReceive().GetPublishedAsync(Arg.Any<int>());
    }
}

public class GetPropertyPhotosQueryHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetPropertyPhotosQueryHandler _handler;

    public GetPropertyPhotosQueryHandlerTests()
    {
        _handler = new GetPropertyPhotosQueryHandler(_properties, _cache, TestMapper.Create());
    }

    private Task<List<ListingPhotoDto>> HandleAsync(int id = 1) =>
        _handler.Handle(new GetPropertyPhotosQuery { PropertyId = id }, default);

    /// <summary>
    /// Rule 79. The proposed photo has not been reviewed and must not appear; the one
    /// waiting to be removed is still on the listing until an admin agrees.
    /// </summary>
    [Fact]
    public async Task Rule_79_a_pending_photo_is_invisible_and_one_waiting_to_go_is_not()
    {
        var listing = TestData.Property(1, status: PropertyStatus.Published, photos:
        [
            TestData.Photo(1, 1, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, 1, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, 1, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        _properties.GetPublishedAsync(1).Returns(listing);

        var photos = await HandleAsync();

        photos.Select(p => p.Id).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task The_gallery_is_in_order()
    {
        var listing = TestData.Property(1, status: PropertyStatus.Published, photos:
        [
            TestData.Photo(7, 1, sortOrder: 3),
            TestData.Photo(8, 1, isPrimary: true, sortOrder: 1),
            TestData.Photo(9, 1, sortOrder: 2)
        ]);

        _properties.GetPublishedAsync(1).Returns(listing);

        (await HandleAsync()).Select(p => p.Id).ShouldBe([8, 9, 7]);
    }

    [Fact]
    public async Task Rule_28_the_gallery_of_a_listing_that_is_not_published_is_not_found()
    {
        _properties.GetPublishedAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync());
    }

    [Fact]
    public async Task The_gallery_is_cached_under_its_own_key()
    {
        _properties.GetPublishedAsync(1).Returns(
            TestData.Property(1, status: PropertyStatus.Published, photos: TestData.Photos(3)));

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.PropertyPhotos(1), Arg.Any<List<ListingPhotoDto>>(), CacheKeys.DetailTtl);
    }
}

public class GetFeaturedPropertiesQueryHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetFeaturedPropertiesQueryHandler _handler;

    public GetFeaturedPropertiesQueryHandlerTests()
    {
        _handler = new GetFeaturedPropertiesQueryHandler(_properties, _cache, TestMapper.Create());
    }

    private Task<List<ListingCardDto>> HandleAsync() =>
        _handler.Handle(new GetFeaturedPropertiesQuery(), default);

    [Fact]
    public async Task Rule_82_at_most_eight_are_asked_for()
    {
        _properties.GetFeaturedAsync(Arg.Any<int>()).Returns([]);

        await HandleAsync();

        await _properties.Received(1).GetFeaturedAsync(8);
    }

    [Fact]
    public async Task The_landing_page_carries_no_distance()
    {
        _properties.GetFeaturedAsync(8).Returns(
            [TestData.Property(1, status: PropertyStatus.Published, photos: TestData.Photos(3))]);

        (await HandleAsync()).Single().DistanceKm.ShouldBeNull();
    }

    [Fact]
    public async Task The_list_is_cached_for_ten_minutes()
    {
        _properties.GetFeaturedAsync(8).Returns([]);

        await HandleAsync();

        await _cache.Received(1).SetAsync(
            CacheKeys.Featured(), Arg.Any<List<ListingCardDto>>(), CacheKeys.FeaturedTtl);
    }

    [Fact]
    public async Task A_cached_list_is_returned_without_touching_the_database()
    {
        _cache.GetAsync<List<ListingCardDto>>(CacheKeys.Featured())
            .Returns([new ListingCardDto { Id = 9, Title = "Cached" }]);

        (await HandleAsync()).Single().Title.ShouldBe("Cached");

        await _properties.DidNotReceive().GetFeaturedAsync(Arg.Any<int>());
    }
}

public class ComparePropertiesQueryHandlerTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly ComparePropertiesQueryHandler _handler;

    public ComparePropertiesQueryHandlerTests()
    {
        _handler = new ComparePropertiesQueryHandler(
            _properties, _locations, new HaversineDistanceCalculator(), TestMapper.Create());
    }

    private static Property Published(int id) =>
        TestData.Property(id, status: PropertyStatus.Published, photos: TestData.Photos(3, id));

    private void Holds(params int[] ids)
    {
        _properties.GetPublishedManyAsync(Arg.Any<IReadOnlyCollection<int>>())
            .Returns(ids.Select(Published).ToList());
    }

    private Task<List<ListingComparisonDto>> HandleAsync(string ids, int? landmarkId = null) =>
        _handler.Handle(new ComparePropertiesQuery { Ids = ids, LandmarkId = landmarkId }, default);

    [Fact]
    public async Task Three_listings_come_back_with_their_facilities()
    {
        Holds(1, 2, 3);

        var listings = await HandleAsync("1,2,3");

        listings.Count.ShouldBe(3);
        listings[0].AddressLine.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rule_35_a_fourth_listing_is_refused()
    {
        await Should.ThrowAsync<BusinessRuleException>(HandleAsync("1,2,3,4"));
    }

    [Fact]
    public async Task Rule_81_a_listing_named_twice_takes_one_slot()
    {
        Holds(1, 2, 3);

        var listings = await HandleAsync("1,1,2,2,3,3");

        listings.Select(l => l.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Rule_81_the_order_given_is_the_order_returned()
    {
        Holds(1, 2, 3);

        (await HandleAsync("3,1,2")).Select(l => l.Id).ShouldBe([3, 1, 2]);
    }

    /// <summary>
    /// A shortlist outlives the listings on it. Refusing all three because one was
    /// unpublished this morning is the least useful answer available.
    /// </summary>
    [Fact]
    public async Task Rule_81_a_listing_that_is_no_longer_published_is_left_out()
    {
        Holds(1, 3);

        (await HandleAsync("1,2,3")).Select(l => l.Id).ShouldBe([1, 3]);
    }

    [Fact]
    public async Task Rule_81_none_of_them_resolving_is_an_empty_comparison_rather_than_an_error()
    {
        Holds();

        (await HandleAsync("1,2,3")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_landmark_measures_each_of_them()
    {
        Holds(1);

        _locations.GetLandmarkAsync(3)
            .Returns(TestData.Landmark(3, latitude: 31.4966, longitude: 74.2996));

        (await HandleAsync("1", landmarkId: 3)).Single().DistanceKm.ShouldBe(6.2);
    }

    [Fact]
    public async Task Rule_85_a_landmark_that_does_not_exist_is_refused()
    {
        _locations.GetLandmarkAsync(404).Returns((Landmark?)null);

        await Should.ThrowAsync<BusinessRuleException>(HandleAsync("1", landmarkId: 404));
    }

    /// <summary>
    /// A set of three arbitrary ids does not repeat the way a filter does, so caching
    /// it would fill Redis with entries read once. Asserted against the constructor
    /// rather than against a substitute, because the point is that there is nothing
    /// to substitute.
    /// </summary>
    [Fact]
    public void A_comparison_is_not_cached()
    {
        typeof(ComparePropertiesQueryHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldNotContain(p => p.ParameterType == typeof(ICacheService));
    }
}
