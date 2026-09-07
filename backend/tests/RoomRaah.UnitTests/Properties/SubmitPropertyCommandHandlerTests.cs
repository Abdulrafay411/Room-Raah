using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class SubmitPropertyCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SubmitPropertyCommandHandler _handler;

    public SubmitPropertyCommandHandlerTests()
    {
        _handler = new SubmitPropertyCommandHandler(
            _properties,
            _unitOfWork,
            Options.Create(TestData.PhotoSettings()),
            TestMapper.Create(),
            NullLogger<SubmitPropertyCommandHandler>.Instance);
    }

    private Property Given(
        PropertyStatus status = PropertyStatus.Draft,
        int photoCount = 3,
        int ownerId = OwnerId)
    {
        var property = TestData.Property(
            PropertyId, ownerId, status, TestData.Photos(photoCount, PropertyId));

        _properties.GetByIdAsync(PropertyId).Returns(property);

        return property;
    }

    private Task<Application.DTOs.OwnerPropertyDto> HandleAsync()
    {
        return _handler.Handle(
            new SubmitPropertyCommand { PropertyId = PropertyId, OwnerId = OwnerId },
            CancellationToken.None);
    }

    [Fact]
    public async Task Rule_14_a_draft_with_enough_photos_moves_to_pending_review()
    {
        var property = Given();

        var result = await HandleAsync();

        property.Status.ShouldBe(PropertyStatus.PendingReview);
        result.Status.ShouldBe("PendingReview");
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_52_a_listing_the_admin_sent_back_can_be_submitted_again()
    {
        var property = Given(PropertyStatus.ChangesRequested);

        await HandleAsync();

        property.Status.ShouldBe(PropertyStatus.PendingReview);
    }

    [Theory]
    [InlineData(PropertyStatus.PendingReview)]
    [InlineData(PropertyStatus.Published)]
    [InlineData(PropertyStatus.Rejected)]
    [InlineData(PropertyStatus.Unpublished)]
    public async Task Rule_52_no_other_status_can_be_submitted(PropertyStatus status)
    {
        var property = Given(status);

        await Should.ThrowAsync<BusinessRuleException>(HandleAsync);

        property.Status.ShouldBe(status);
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Rule_15_fewer_than_three_photos_is_refused(int photoCount)
    {
        Given(photoCount: photoCount);

        var error = await Should.ThrowAsync<BusinessRuleException>(HandleAsync);

        error.Message.ShouldContain("3");
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_15_exactly_three_photos_is_enough()
    {
        var property = Given(photoCount: 3);

        await HandleAsync();

        property.Status.ShouldBe(PropertyStatus.PendingReview);
    }

    [Fact]
    public async Task Rule_15_fifteen_photos_is_still_allowed()
    {
        var property = Given(photoCount: 15);

        await HandleAsync();

        property.Status.ShouldBe(PropertyStatus.PendingReview);
    }

    [Fact]
    public async Task Rule_15_more_than_fifteen_photos_is_refused()
    {
        Given(photoCount: 16);

        var error = await Should.ThrowAsync<BusinessRuleException>(HandleAsync);

        error.Message.ShouldContain("15");
    }

    /// <summary>
    /// Rule 15 counts Live photos. A photo still waiting to be approved onto a
    /// published listing is not one a visitor would see, so it cannot be what gets
    /// a listing over the minimum.
    /// </summary>
    [Fact]
    public async Task Only_live_photos_count_towards_the_minimum()
    {
        var photos = TestData.Photos(2, PropertyId);
        photos.Add(TestData.Photo(3, PropertyId, sortOrder: 3, status: PhotoStatus.PendingAdd));

        _properties.GetByIdAsync(PropertyId).Returns(
            TestData.Property(PropertyId, OwnerId, PropertyStatus.Draft, photos));

        await Should.ThrowAsync<BusinessRuleException>(HandleAsync);
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync);
    }

    [Fact]
    public async Task Rule_18_another_owners_listing_may_not_be_submitted()
    {
        var property = Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(HandleAsync);

        property.Status.ShouldBe(PropertyStatus.Draft);
    }

    [Fact]
    public async Task Submitting_stamps_the_listing_as_changed()
    {
        var property = Given();
        property.UpdatedAt = DateTime.UtcNow.AddDays(-3);

        await HandleAsync();

        property.UpdatedAt.ShouldBe(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
