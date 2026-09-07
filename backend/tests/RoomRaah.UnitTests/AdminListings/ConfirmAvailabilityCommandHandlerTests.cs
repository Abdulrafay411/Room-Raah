using FluentValidation;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.AdminListings;

public class ConfirmAvailabilityCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ConfirmAvailabilityCommandHandler _handler;

    private readonly List<PropertyRevision> _created = [];

    public ConfirmAvailabilityCommandHandlerTests()
    {
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_created.Add)).Returns(Task.CompletedTask);

        _handler = new ConfirmAvailabilityCommandHandler(
            _properties, _revisions, _cache, _unitOfWork, TestMapper.Create());
    }

    private Property Given(PropertyStatus status = PropertyStatus.Published, int ownerId = OwnerId)
    {
        var property = TestData.Property(PropertyId, ownerId, status, TestData.Photos(3, PropertyId));
        property.AvailabilityConfirmedAt = DateTime.UtcNow.AddDays(-30);
        _properties.GetByIdAsync(PropertyId).Returns(property);
        return property;
    }

    private Task<OwnerListingUpdateDto> HandleAsync(int? beds = 1) =>
        _handler.Handle(new ConfirmAvailabilityCommand
        {
            PropertyId = PropertyId,
            OwnerId = OwnerId,
            AvailableBeds = beds
        }, CancellationToken.None);

    [Fact]
    public async Task Rule_21_a_published_listing_gets_an_availability_revision()
    {
        var property = Given();

        var result = await HandleAsync(1);

        _created.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Availability);
        _created[0].AvailableBeds.ShouldBe(1);
        result.PendingRevision.ShouldNotBeNull();

        // Rule 19: the number on the public page has not moved.
        property.AvailableBeds.ShouldBe(2);
    }

    /// <summary>
    /// Rule 61's deliberate exception. Confirming that the count is still right is
    /// the entire purpose of this endpoint, so an unchanged number is accepted.
    /// </summary>
    [Fact]
    public async Task Rule_61_re_confirming_an_unchanged_count_is_accepted()
    {
        var property = Given();

        await HandleAsync(property.AvailableBeds);

        _created.ShouldHaveSingleItem().AvailableBeds.ShouldBe(2);
    }

    [Fact]
    public async Task Rule_20_a_second_availability_revision_is_refused()
    {
        var property = Given();
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Availability)
            .Returns(TestData.Revision(property, RevisionKind.Availability));

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(0));
    }

    [Fact]
    public async Task A_listing_that_is_not_published_is_confirmed_in_place()
    {
        var property = Given(PropertyStatus.Draft);
        var before = property.AvailabilityConfirmedAt;

        var result = await HandleAsync(0);

        property.AvailableBeds.ShouldBe(0);
        property.AvailabilityConfirmedAt.ShouldBeGreaterThan(before);
        result.PendingRevision.ShouldBeNull();
        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    /// <summary>
    /// The upper bound is the listing's own total, which the validator cannot see.
    /// It is still a rule about the shape of the request, so it answers 400 like the
    /// rest of the field validation table rather than 422.
    /// </summary>
    [Fact]
    public async Task More_free_beds_than_the_room_has_is_a_validation_failure()
    {
        Given();

        var failure = await Should.ThrowAsync<ValidationException>(() => HandleAsync(99));

        failure.Errors.ShouldContain(e => e.PropertyName == "AvailableBeds");
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_60_a_listing_under_review_refuses_the_confirmation()
    {
        Given(PropertyStatus.PendingReview);

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(1));
    }

    [Fact]
    public async Task Rule_18_another_owners_listing_may_not_be_confirmed()
    {
        Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync(1));
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(1));
    }
}
