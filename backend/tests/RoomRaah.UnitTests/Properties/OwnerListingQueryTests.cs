using FluentValidation.TestHelper;
using NSubstitute;
using RoomRaah.Application.Features.Properties.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class GetMyPropertiesQueryHandlerTests
{
    private const int OwnerId = 7;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly GetMyPropertiesQueryHandler _handler;

    public GetMyPropertiesQueryHandlerTests()
    {
        _handler = new GetMyPropertiesQueryHandler(_properties, TestMapper.Create());
    }

    private Task<Application.DTOs.PagedResult<Application.DTOs.OwnerPropertySummaryDto>> HandleAsync(
        int page = 1, int pageSize = 12)
    {
        return _handler.Handle(
            new GetMyPropertiesQuery { OwnerId = OwnerId, Page = page, PageSize = pageSize },
            CancellationToken.None);
    }

    [Fact]
    public async Task Only_the_signed_in_owners_listings_are_asked_for()
    {
        _properties.GetByOwnerAsync(OwnerId, 2, 5).Returns([]);
        _properties.CountByOwnerAsync(OwnerId).Returns(0);

        await HandleAsync(2, 5);

        await _properties.Received(1).GetByOwnerAsync(OwnerId, 2, 5);
        await _properties.Received(1).CountByOwnerAsync(OwnerId);
    }

    [Fact]
    public async Task The_page_carries_the_total_across_every_page()
    {
        _properties.GetByOwnerAsync(OwnerId, 1, 12).Returns([TestData.Property(1, OwnerId)]);
        _properties.CountByOwnerAsync(OwnerId).Returns(31);

        var result = await HandleAsync();

        result.TotalCount.ShouldBe(31);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(12);
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task An_owner_with_no_listings_gets_an_empty_page_rather_than_nothing()
    {
        _properties.GetByOwnerAsync(OwnerId, 1, 12).Returns([]);
        _properties.CountByOwnerAsync(OwnerId).Returns(0);

        var result = await HandleAsync();

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_row_carries_its_photo_count_and_the_primary_thumbnail()
    {
        var property = TestData.Property(1, OwnerId, PropertyStatus.Published, TestData.Photos(3));
        _properties.GetByOwnerAsync(OwnerId, 1, 12).Returns([property]);

        var result = await HandleAsync();

        var row = result.Items.Single();
        row.PhotoCount.ShouldBe(3);
        row.PrimaryThumbnailUrl.ShouldBe(property.Photos.Single(p => p.IsPrimary).ThumbnailUrl);
        row.Status.ShouldBe("Published");
        row.AreaName.ShouldBe("Gulberg");
        row.CityName.ShouldBe("Lahore");
    }

    [Fact]
    public async Task A_listing_with_no_photos_yet_reports_no_thumbnail()
    {
        _properties.GetByOwnerAsync(OwnerId, 1, 12).Returns([TestData.Property(1, OwnerId)]);

        var result = await HandleAsync();

        result.Items.Single().PhotoCount.ShouldBe(0);
        result.Items.Single().PrimaryThumbnailUrl.ShouldBeNull();
    }
}

public class GetMyPropertiesQueryValidatorTests
{
    private readonly GetMyPropertiesQueryValidator _validator = new();

    [Fact]
    public void The_defaults_pass()
    {
        _validator.TestValidate(new GetMyPropertiesQuery()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Page_size_defaults_to_twelve()
    {
        new GetMyPropertiesQuery().PageSize.ShouldBe(12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rule_30_a_page_below_one_is_refused(int page)
    {
        _validator.TestValidate(new GetMyPropertiesQuery { Page = page })
            .ShouldHaveValidationErrorFor("page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Rule_30_a_page_size_outside_one_to_fifty_is_refused(int pageSize)
    {
        _validator.TestValidate(new GetMyPropertiesQuery { PageSize = pageSize })
            .ShouldHaveValidationErrorFor("pageSize");
    }

    [Fact]
    public void Rule_30_fifty_is_the_largest_page_allowed()
    {
        _validator.TestValidate(new GetMyPropertiesQuery { PageSize = 50 })
            .ShouldNotHaveAnyValidationErrors();
    }
}

public class GetMyPropertyQueryHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly GetMyPropertyQueryHandler _handler;

    public GetMyPropertyQueryHandlerTests()
    {
        _handler = new GetMyPropertyQueryHandler(_properties, TestMapper.Create());
    }

    private Task<Application.DTOs.OwnerPropertyDto> HandleAsync()
    {
        return _handler.Handle(
            new GetMyPropertyQuery { PropertyId = PropertyId, OwnerId = OwnerId },
            CancellationToken.None);
    }

    [Fact]
    public async Task The_listing_comes_back_with_its_photos_in_gallery_order()
    {
        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Draft,
        [
            TestData.Photo(1, PropertyId, sortOrder: 3),
            TestData.Photo(2, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(3, PropertyId, sortOrder: 2)
        ]);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        var result = await HandleAsync();

        result.Photos.Select(p => p.Id).ShouldBe([2, 3, 1]);
    }

    [Fact]
    public async Task The_listing_carries_the_pin_the_owner_dropped()
    {
        _properties.GetByIdAsync(PropertyId).Returns(TestData.Property(PropertyId, OwnerId));

        var result = await HandleAsync();

        result.Latitude.ShouldBe(31.5204);
        result.Longitude.ShouldBe(74.3587);
        result.AddressLine.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync);
    }

    [Fact]
    public async Task Rule_18_another_owners_listing_may_not_be_read()
    {
        _properties.GetByIdAsync(PropertyId).Returns(TestData.Property(PropertyId, OwnerId + 1));

        await Should.ThrowAsync<UnauthorizedAccessException>(HandleAsync);
    }
}
