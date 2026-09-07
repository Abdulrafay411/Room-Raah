using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Properties;

public class UploadPropertyPhotoCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;
    private const string Original = "http://blobs.local/properties/11/new.jpg";
    private const string Thumbnail = "http://blobs.local/properties/11/thumb_new.jpg";

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyPhotoRepository _photos = Substitute.For<IPropertyPhotoRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly IPhotoStorageService _storage = Substitute.For<IPhotoStorageService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UploadPropertyPhotoCommandHandler _handler;

    private PropertyPhoto? _saved;
    private readonly List<PropertyRevision> _createdRevisions = [];

    public UploadPropertyPhotoCommandHandlerTests()
    {
        _storage.UploadAsync(Arg.Any<int>(), Arg.Any<Stream>(), Arg.Any<string>())
            .Returns(new StoredPhoto(Original, Thumbnail));

        _photos.CreateAsync(Arg.Do<PropertyPhoto>(p => _saved = p)).Returns(Task.CompletedTask);
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_createdRevisions.Add)).Returns(Task.CompletedTask);

        _handler = new UploadPropertyPhotoCommandHandler(
            _properties, _photos, _revisions, _storage, _unitOfWork,
            Options.Create(TestData.PhotoSettings()), TestMapper.Create());
    }

    private Property Given(
        PropertyStatus status = PropertyStatus.Draft,
        int photoCount = 0,
        int ownerId = OwnerId)
    {
        var property = TestData.Property(
            PropertyId, ownerId, status, TestData.Photos(photoCount, PropertyId));

        _properties.GetByIdAsync(PropertyId).Returns(property);

        return property;
    }

    private Task<Application.DTOs.PropertyPhotoDto> HandleAsync()
    {
        return _handler.Handle(new UploadPropertyPhotoCommand
        {
            PropertyId = PropertyId,
            OwnerId = OwnerId,
            FileName = "room.jpg",
            ContentType = "image/jpeg",
            Length = 120_000,
            Content = new MemoryStream([1, 2, 3])
        }, CancellationToken.None);
    }

    [Fact]
    public async Task The_stored_urls_are_what_the_row_carries()
    {
        Given();

        var result = await HandleAsync();

        _saved.ShouldNotBeNull();
        _saved!.BlobUrl.ShouldBe(Original);
        _saved.ThumbnailUrl.ShouldBe(Thumbnail);
        result.Url.ShouldBe(Original);
        result.ThumbnailUrl.ShouldBe(Thumbnail);
    }

    [Fact]
    public async Task Rule_59_a_photo_on_an_unpublished_listing_is_live()
    {
        Given();

        var result = await HandleAsync();

        _saved!.Status.ShouldBe(PhotoStatus.Live);
        result.Status.ShouldBe("Live");
    }

    [Fact]
    public async Task Rule_57_the_first_photo_becomes_the_primary_one()
    {
        Given(photoCount: 0);

        await HandleAsync();

        _saved!.IsPrimary.ShouldBeTrue();
        _saved.SortOrder.ShouldBe(1);
    }

    [Fact]
    public async Task Rule_57_a_later_photo_does_not_take_the_primary_slot()
    {
        Given(photoCount: 2);

        await HandleAsync();

        _saved!.IsPrimary.ShouldBeFalse();
        _saved.SortOrder.ShouldBe(3);
    }

    [Fact]
    public async Task A_new_photo_goes_after_the_highest_position_in_use()
    {
        var property = Given(photoCount: 0);
        property.Photos.Add(TestData.Photo(9, PropertyId, isPrimary: true, sortOrder: 7));

        await HandleAsync();

        _saved!.SortOrder.ShouldBe(8);
    }

    [Fact]
    public async Task Rule_55_the_sixteenth_photo_is_refused_before_anything_is_uploaded()
    {
        Given(photoCount: 15);

        var error = await Should.ThrowAsync<BusinessRuleException>(HandleAsync);

        error.Message.ShouldContain("15");
        await _storage.DidNotReceive().UploadAsync(Arg.Any<int>(), Arg.Any<Stream>(), Arg.Any<string>());
    }

        /// <summary>
    /// Rules 66 and 67. On a published listing the upload is a proposal: it is stored
    /// and it is queued, but it is not on the page until an admin says so.
    /// </summary>
    [Fact]
    public async Task Rule_66_a_photo_added_to_a_published_listing_is_proposed_not_live()
    {
        Given(PropertyStatus.Published, photoCount: 3);

        await HandleAsync();

        _saved!.Status.ShouldBe(PhotoStatus.PendingAdd);
        _saved.IsPrimary.ShouldBeFalse();
        _createdRevisions.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Content);
    }

    [Fact]
    public async Task Rule_67_a_content_revision_already_open_is_joined_rather_than_duplicated()
    {
        var property = Given(PropertyStatus.Published, photoCount: 3);
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Content)
            .Returns(Application.Listings.ListingRevisions.Snapshot(property, RevisionKind.Content));

        await HandleAsync();

        _createdRevisions.ShouldBeEmpty();
    }

    /// <summary>
    /// Rule 55 counts the gallery the listing will end up with. A photo already on
    /// its way out is not taking one of the fifteen places.
    /// </summary>
    [Fact]
    public async Task Rule_55_a_photo_waiting_to_be_removed_does_not_take_a_place()
    {
        var photos = TestData.Photos(15, PropertyId);
        photos[14].Status = PhotoStatus.PendingRemove;

        var property = TestData.Property(PropertyId, OwnerId, PropertyStatus.Published, photos);
        _properties.GetByIdAsync(PropertyId).Returns(property);

        await HandleAsync();

        _saved.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.PendingReview)]
    [InlineData(PropertyStatus.ChangesRequested)]
    [InlineData(PropertyStatus.Rejected)]
    [InlineData(PropertyStatus.Unpublished)]
    public async Task Rule_54_every_status_but_published_accepts_a_photo(PropertyStatus status)
    {
        Given(status);

        await HandleAsync();

        _saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rule_56_bytes_that_are_not_a_readable_image_are_refused()
    {
        Given();
        _storage.UploadAsync(Arg.Any<int>(), Arg.Any<Stream>(), Arg.Any<string>())
            .Returns((StoredPhoto?)null);

        var error = await Should.ThrowAsync<BusinessRuleException>(HandleAsync);

        error.Message.ShouldContain("JPEG");
        await _photos.DidNotReceive().CreateAsync(Arg.Any<PropertyPhoto>());
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(HandleAsync);
    }

    [Fact]
    public async Task Rule_18_a_photo_cannot_be_added_to_another_owners_listing()
    {
        Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(HandleAsync);

        await _storage.DidNotReceive().UploadAsync(Arg.Any<int>(), Arg.Any<Stream>(), Arg.Any<string>());
    }

    /// <summary>
    /// The file reaches blob storage before the row that names it reaches the
    /// database. If the save fails, the file is the thing left behind, and nothing
    /// else in the product will ever be able to find it again.
    /// </summary>
    [Fact]
    public async Task A_failed_save_takes_the_uploaded_file_back_out_of_storage()
    {
        Given();
        _unitOfWork.SaveChangesAsync().Throws(new InvalidOperationException("the database went away"));

        await Should.ThrowAsync<InvalidOperationException>(HandleAsync);

        await _storage.Received(1).DeleteAsync(Original, Thumbnail);
    }
}
