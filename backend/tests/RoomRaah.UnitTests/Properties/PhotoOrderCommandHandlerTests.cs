using NSubstitute;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class ReorderPropertyPhotosCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReorderPropertyPhotosCommandHandler _handler;

    private readonly List<PropertyRevision> _created = [];

    public ReorderPropertyPhotosCommandHandlerTests()
    {
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_created.Add)).Returns(Task.CompletedTask);

        _handler = new ReorderPropertyPhotosCommandHandler(
            _properties, _revisions, _unitOfWork, TestMapper.Create());
    }

    private Property Given(
        PropertyStatus status = PropertyStatus.Draft,
        int ownerId = OwnerId,
        List<PropertyPhoto>? photos = null)
    {
        var property = TestData.Property(PropertyId, ownerId, status, photos ?? TestData.Photos(3, PropertyId));
        _properties.GetByIdAsync(PropertyId).Returns(property);
        return property;
    }

    private Task<OwnerListingUpdateDto> HandleAsync(params int[] photoIds)
    {
        return _handler.Handle(new ReorderPropertyPhotosCommand
        {
            PropertyId = PropertyId,
            OwnerId = OwnerId,
            PhotoIds = photoIds.ToList()
        }, CancellationToken.None);
    }

    [Fact]
    public async Task The_order_given_becomes_positions_one_upwards()
    {
        var property = Given();

        await HandleAsync(3, 1, 2);

        property.Photos.First(p => p.Id == 3).SortOrder.ShouldBe(1);
        property.Photos.First(p => p.Id == 1).SortOrder.ShouldBe(2);
        property.Photos.First(p => p.Id == 2).SortOrder.ShouldBe(3);
    }

    [Fact]
    public async Task The_response_comes_back_in_the_new_order()
    {
        Given();

        var result = await HandleAsync(3, 1, 2);

        result.Listing.Photos.Select(p => p.Id).ShouldBe([3, 1, 2]);
        result.PendingRevision.ShouldBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Reordering_does_not_change_which_photo_is_primary()
    {
        var property = Given();

        await HandleAsync(3, 2, 1);

        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(1);
    }

    [Fact]
    public async Task Rule_58_a_list_that_leaves_a_photo_out_is_refused()
    {
        Given();

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(1, 2));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_58_a_list_naming_a_photo_that_is_not_here_is_refused()
    {
        Given();

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(1, 2, 99));
    }

    [Fact]
    public async Task Rule_58_a_list_naming_the_same_photo_twice_is_refused()
    {
        Given();

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(1, 1, 2));
    }

    [Fact]
    public async Task Rule_67_a_published_listing_gets_a_content_revision_instead()
    {
        var property = Given(PropertyStatus.Published);

        var result = await HandleAsync(3, 1, 2);

        _created.ShouldHaveSingleItem();
        _created[0].Kind.ShouldBe(RevisionKind.Content);
        _created[0].PhotoOrder.ShouldBe("3,1,2");
        result.PendingRevision.ShouldNotBeNull().ProposedPhotoOrder.ShouldBe([3, 1, 2]);
    }

    [Fact]
    public async Task Rule_19_the_live_gallery_does_not_move_while_the_revision_waits()
    {
        var property = Given(PropertyStatus.Published);

        await HandleAsync(3, 1, 2);

        property.Photos.First(p => p.Id == 1).SortOrder.ShouldBe(1);
        property.Photos.First(p => p.Id == 3).SortOrder.ShouldBe(3);
    }

    [Fact]
    public async Task Rule_67_a_revision_already_open_is_joined_rather_than_duplicated()
    {
        var property = Given(PropertyStatus.Published);
        var open = ListingRevisions.Snapshot(property, RevisionKind.Content);
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Content).Returns(open);

        await HandleAsync(2, 3, 1);

        _created.ShouldBeEmpty();
        open.PhotoOrder.ShouldBe("2,3,1");
    }

    /// <summary>
    /// Rule 68. On a published listing the order names the gallery the revision will
    /// produce, so a photo already on its way out is not part of it and a proposed
    /// one is.
    /// </summary>
    [Fact]
    public async Task Rule_68_the_order_names_the_gallery_the_revision_will_produce()
    {
        Given(PropertyStatus.Published,
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, PropertyId, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(1, 2, 3));

        await HandleAsync(3, 1);
    }

    [Fact]
    public async Task Rule_18_another_owners_photos_may_not_be_reordered()
    {
        Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync(3, 1, 2));
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(1));
    }

    private Property Given(PropertyStatus status, List<PropertyPhoto> photos) =>
        Given(status, OwnerId, photos);
}

public class SetPrimaryPropertyPhotoCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SetPrimaryPropertyPhotoCommandHandler _handler;

    private readonly List<PropertyRevision> _created = [];

    public SetPrimaryPropertyPhotoCommandHandlerTests()
    {
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_created.Add)).Returns(Task.CompletedTask);

        _handler = new SetPrimaryPropertyPhotoCommandHandler(
            _properties, _revisions, _unitOfWork, TestMapper.Create());
    }

    private Property Given(PropertyStatus status = PropertyStatus.Draft, int ownerId = OwnerId)
    {
        var property = TestData.Property(PropertyId, ownerId, status, TestData.Photos(3, PropertyId));
        _properties.GetByIdAsync(PropertyId).Returns(property);
        return property;
    }

    private Task<OwnerListingUpdateDto> HandleAsync(int photoId = 2)
    {
        return _handler.Handle(new SetPrimaryPropertyPhotoCommand
        {
            PropertyId = PropertyId,
            PhotoId = photoId,
            OwnerId = OwnerId
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Rule_57_the_chosen_photo_becomes_the_only_primary_one()
    {
        var property = Given();

        await HandleAsync(3);

        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(3);
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 57 says exactly one, so the handler assigns every photo rather than
    /// swapping two. Data that somehow arrived with two primaries is corrected by
    /// the next choice rather than carried forward.
    /// </summary>
    [Fact]
    public async Task Rule_57_two_primaries_collapse_to_one()
    {
        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Draft,
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, isPrimary: true, sortOrder: 2)
        ]);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        await HandleAsync(2);

        property.Photos.Count(p => p.IsPrimary).ShouldBe(1);
        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(2);
    }

    [Fact]
    public async Task Choosing_the_photo_that_is_already_primary_changes_nothing_else()
    {
        var property = Given();

        await HandleAsync(1);

        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(1);
    }

    [Fact]
    public async Task A_photo_that_is_not_on_this_listing_is_not_found()
    {
        var property = Given();

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(99));

        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(1);
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_67_a_published_listing_proposes_the_new_primary_instead()
    {
        var property = Given(PropertyStatus.Published);

        var result = await HandleAsync(3);

        _created.ShouldHaveSingleItem();
        _created[0].PrimaryPhotoId.ShouldBe(3);
        result.PendingRevision.ShouldNotBeNull();

        // Rule 19: the published card still shows the photo it was approved with.
        property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(1);
    }

    /// <summary>
    /// A proposed order and a proposed primary are two separate things. Choosing a
    /// cover photograph must not silently rearrange a gallery the owner has already
    /// asked to reorder.
    /// </summary>
    [Fact]
    public async Task Choosing_a_primary_leaves_a_proposed_order_alone()
    {
        var property = Given(PropertyStatus.Published);
        var open = ListingRevisions.Snapshot(property, RevisionKind.Content);
        open.PhotoOrder = "3,2,1";
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Content).Returns(open);

        await HandleAsync(2);

        open.PhotoOrder.ShouldBe("3,2,1");
        open.PrimaryPhotoId.ShouldBe(2);
    }

    [Fact]
    public async Task Rule_18_another_owners_photo_may_not_be_made_primary()
    {
        Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync());
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync());
    }
}
