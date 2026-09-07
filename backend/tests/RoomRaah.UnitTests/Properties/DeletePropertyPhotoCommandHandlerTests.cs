using NSubstitute;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class DeletePropertyPhotoCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyPhotoRepository _photos = Substitute.For<IPropertyPhotoRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly IPhotoStorageService _storage = Substitute.For<IPhotoStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly DeletePropertyPhotoCommandHandler _handler;
    private readonly List<PropertyRevision> _createdRevisions = [];

    public DeletePropertyPhotoCommandHandlerTests()
    {
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_createdRevisions.Add)).Returns(Task.CompletedTask);

        _handler = new DeletePropertyPhotoCommandHandler(
            _properties, _photos, _revisions, _storage, _unitOfWork);
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

    private Task HandleAsync(int photoId = 1)
    {
        return _handler.Handle(new DeletePropertyPhotoCommand
        {
            PropertyId = PropertyId,
            PhotoId = photoId,
            OwnerId = OwnerId
        }, CancellationToken.None);
    }

    [Fact]
    public async Task The_row_is_removed_and_then_its_files_are()
    {
        var property = Given();
        var photo = property.Photos.First(p => p.Id == 2);

        await HandleAsync(2);

        await _photos.Received(1).DeleteAsync(photo);
        await _unitOfWork.Received(1).SaveChangesAsync();
        await _storage.Received(1).DeleteAsync(photo.BlobUrl, photo.ThumbnailUrl);
    }

    /// <summary>
    /// Order matters. A file removed before a save that then fails leaves a row
    /// pointing at nothing, which is the one failure a visitor actually sees.
    /// </summary>
    [Fact]
    public async Task The_files_are_removed_only_after_the_save()
    {
        Given();
        var order = new List<string>();

        _unitOfWork.SaveChangesAsync().Returns(_ => { order.Add("save"); return 1; });
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(_ => { order.Add("blob"); return Task.CompletedTask; });

        await HandleAsync(2);

        order.ShouldBe(["save", "blob"]);
    }

    [Fact]
    public async Task Rule_57_removing_the_primary_photo_promotes_the_next_one()
    {
        var property = Given(photoCount: 3);

        await HandleAsync(1);

        property.Photos.First(p => p.Id == 2).IsPrimary.ShouldBeTrue();
        property.Photos.First(p => p.Id == 3).IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public async Task Rule_57_the_successor_is_the_lowest_position_that_is_left()
    {
        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Draft,
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 9),
            TestData.Photo(3, PropertyId, sortOrder: 4)
        ]);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        await HandleAsync(1);

        property.Photos.First(p => p.Id == 3).IsPrimary.ShouldBeTrue();
        property.Photos.First(p => p.Id == 2).IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public async Task Removing_a_photo_that_was_not_primary_leaves_the_primary_alone()
    {
        var property = Given();

        await HandleAsync(3);

        property.Photos.First(p => p.Id == 1).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public async Task Removing_the_only_photo_leaves_nothing_to_promote()
    {
        var property = Given(photoCount: 1);

        await HandleAsync(1);

        property.Photos.ShouldBeEmpty();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Dropping_below_three_photos_is_allowed()
    {
        Given(photoCount: 3);

        await HandleAsync(3);

        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task A_photo_that_is_not_on_this_listing_is_not_found()
    {
        Given();

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(99));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync());
    }

        /// <summary>
    /// Rule 66. On a published listing a deletion is a proposal: the photo is still
    /// on the page the public is looking at, so nothing is removed and no file is
    /// touched until an admin agrees.
    /// </summary>
    [Fact]
    public async Task Rule_66_a_published_listing_marks_the_photo_for_removal_instead()
    {
        var property = Given(PropertyStatus.Published);

        await HandleAsync(2);

        property.Photos.Count.ShouldBe(3);
        property.Photos.Single(p => p.Id == 2).Status.ShouldBe(PhotoStatus.PendingRemove);

        await _photos.DidNotReceive().DeleteAsync(Arg.Any<PropertyPhoto>());
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Rule_67_the_removal_opens_a_content_revision()
    {
        Given(PropertyStatus.Published);

        await HandleAsync(2);

        _createdRevisions.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Content);
    }

    /// <summary>
    /// Rule 66 again, from the other side. A photo that has never been public has
    /// nothing for an admin to weigh, so withdrawing it really deletes it.
    /// </summary>
    [Fact]
    public async Task Rule_66_withdrawing_a_proposed_photo_deletes_it_outright()
    {
        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Published,
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2),
            TestData.Photo(3, PropertyId, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        await HandleAsync(3);

        property.Photos.Count.ShouldBe(2);
        await _storage.Received(1).DeleteAsync(
            "http://blobs.local/properties/11/photo3.jpg",
            "http://blobs.local/properties/11/thumb3.jpg");
    }

    [Fact]
    public async Task A_photo_already_waiting_to_be_removed_is_refused()
    {
        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Published,
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove)
        ]);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(2));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_18_another_owners_photo_may_not_be_deleted()
    {
        Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync());

        await _photos.DidNotReceive().DeleteAsync(Arg.Any<PropertyPhoto>());
    }
}
