using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminListings.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.AdminListings;

public class RevisionDecisionHandlerTests
{
    private const int AdminId = 3;
    private const int OwnerId = 7;
    private const int PropertyId = 11;
    private const int RevisionId = 511;

    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly IPropertyPhotoRepository _photos = Substitute.For<IPropertyPhotoRepository>();
    private readonly IPhotoStorageService _storage = Substitute.For<IPhotoStorageService>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ApproveRevisionCommandHandler _approve;
    private readonly RejectRevisionCommandHandler _reject;

    public RevisionDecisionHandlerTests()
    {
        var mapper = TestMapper.Create();

        _approve = new ApproveRevisionCommandHandler(
            _revisions, _photos, _storage, _cache, _email, _unitOfWork, mapper,
            NullLogger<ApproveRevisionCommandHandler>.Instance);

        _reject = new RejectRevisionCommandHandler(
            _revisions, _photos, _storage, _cache, _email, _unitOfWork, mapper,
            NullLogger<RejectRevisionCommandHandler>.Instance);
    }

    private PropertyRevision Given(
        RevisionKind kind = RevisionKind.Content,
        Action<PropertyRevision>? change = null,
        List<PropertyPhoto>? photos = null)
    {
        var property = TestData.ReviewableProperty(
            PropertyId, OwnerId, PropertyStatus.Published, photos: photos);

        var revision = TestData.Revision(property, kind, change);
        revision.Id = RevisionId;

        _revisions.GetByIdAsync(RevisionId).Returns(revision);

        return revision;
    }

    private Task<RevisionDetailDto> ApproveAsync(string? note = null) =>
        _approve.Handle(
            new ApproveRevisionCommand { RevisionId = RevisionId, AdminId = AdminId, AdminNote = note },
            CancellationToken.None);

    private Task<RevisionDetailDto> RejectAsync(string? note) =>
        _reject.Handle(
            new RejectRevisionCommand { RevisionId = RevisionId, AdminId = AdminId, AdminNote = note },
            CancellationToken.None);

    [Fact]
    public async Task Rule_25_approving_copies_the_proposed_fields_onto_the_listing()
    {
        var revision = Given(change: r =>
        {
            r.Title = "A quieter room on the same street";
            r.MonthlyRent = 21000m;
        });

        var result = await ApproveAsync();

        revision.Property.Title.ShouldBe("A quieter room on the same street");
        revision.Property.MonthlyRent.ShouldBe(21000m);
        revision.ReviewStatus.ShouldBe(RevisionStatus.Approved);
        revision.ReviewedByAdminId.ShouldBe(AdminId);
        revision.ReviewedAt.ShouldNotBeNull();
        result.ReviewStatus.ShouldBe("Approved");
    }

    [Fact]
    public async Task Rule_25_approving_replaces_the_facility_list()
    {
        var revision = Given(change: r => r.FacilityIds = "2,5");

        await ApproveAsync();

        revision.Property.Facilities.Select(f => f.FacilityId).OrderBy(id => id).ShouldBe([2, 5]);
    }

    /// <summary>
    /// Rule 73. A content revision holds the bed count as it was when the owner wrote
    /// it, and it does not own that field. An availability revision approved in
    /// between must not be undone by approving this one.
    /// </summary>
    [Fact]
    public async Task Rule_73_a_content_revision_does_not_write_the_bed_count_it_captured()
    {
        var revision = Given(change: r =>
        {
            r.Title = "Renamed";
            r.AvailableBeds = 2;
        });

        revision.Property.AvailableBeds = 0;

        await ApproveAsync();

        revision.Property.AvailableBeds.ShouldBe(0);
    }

    [Fact]
    public async Task Rule_73_an_availability_revision_writes_the_bed_count_and_nothing_else()
    {
        var revision = Given(RevisionKind.Availability, r =>
        {
            r.AvailableBeds = 0;
            r.Title = "This should be ignored";
        });

        revision.Property.AvailabilityConfirmedAt = DateTime.UtcNow.AddDays(-30);
        var titleBefore = revision.Property.Title;

        await ApproveAsync();

        revision.Property.AvailableBeds.ShouldBe(0);
        revision.Property.Title.ShouldBe(titleBefore);
        revision.Property.AvailabilityConfirmedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Rule_69_approving_turns_a_proposed_photo_live_and_deletes_a_withdrawn_one()
    {
        var revision = Given(photos:
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, PropertyId, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        await ApproveAsync();

        var photos = revision.Property.Photos;

        photos.Select(p => p.Id).OrderBy(id => id).ShouldBe([1, 3]);
        photos.ShouldAllBe(p => p.Status == PhotoStatus.Live);

        await _storage.Received(1).DeleteAsync(
            "http://blobs.local/properties/11/photo2.jpg",
            "http://blobs.local/properties/11/thumb2.jpg");
    }

    [Fact]
    public async Task Rule_69_rejecting_deletes_the_proposed_photo_and_keeps_the_withdrawn_one()
    {
        var revision = Given(photos:
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove),
            TestData.Photo(3, PropertyId, sortOrder: 3, status: PhotoStatus.PendingAdd)
        ]);

        await RejectAsync("The new photo is of a different room.");

        var photos = revision.Property.Photos;

        photos.Select(p => p.Id).OrderBy(id => id).ShouldBe([1, 2]);
        photos.ShouldAllBe(p => p.Status == PhotoStatus.Live);

        await _storage.Received(1).DeleteAsync(
            "http://blobs.local/properties/11/photo3.jpg",
            "http://blobs.local/properties/11/thumb3.jpg");
    }

    [Fact]
    public async Task Rule_68_approving_lays_the_gallery_out_in_the_order_that_was_proposed()
    {
        var revision = Given(change: r =>
        {
            r.PhotoOrder = "3,1,2";
            r.PrimaryPhotoId = 2;
        });

        await ApproveAsync();

        var photos = revision.Property.Photos.OrderBy(p => p.SortOrder).ToList();

        photos.Select(p => p.Id).ShouldBe([3, 1, 2]);
        photos.Single(p => p.IsPrimary).Id.ShouldBe(2);
    }

    /// <summary>
    /// Rule 57 has to survive a proposal that named a photo which then left. The one
    /// at the front of the order keeps the flag rather than the listing ending up
    /// with none.
    /// </summary>
    [Fact]
    public async Task A_proposed_primary_that_is_no_longer_there_leaves_exactly_one_primary()
    {
        var revision = Given(
            change: r =>
            {
                r.PhotoOrder = "1,3";
                r.PrimaryPhotoId = 2;
            },
            photos:
            [
                TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
                TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove),
                TestData.Photo(3, PropertyId, sortOrder: 3)
            ]);

        await ApproveAsync();

        revision.Property.Photos.Count(p => p.IsPrimary).ShouldBe(1);
        revision.Property.Photos.Single(p => p.IsPrimary).Id.ShouldBe(1);
    }

    [Fact]
    public async Task Rule_27_rejecting_leaves_the_live_listing_exactly_as_it_was()
    {
        var revision = Given(change: r =>
        {
            r.Title = "Never applied";
            r.MonthlyRent = 99000m;
        });

        var titleBefore = revision.Property.Title;
        var rentBefore = revision.Property.MonthlyRent;

        await RejectAsync("Not what was agreed.");

        revision.Property.Title.ShouldBe(titleBefore);
        revision.Property.MonthlyRent.ShouldBe(rentBefore);
        revision.ReviewStatus.ShouldBe(RevisionStatus.Rejected);
    }

    [Fact]
    public async Task Rule_26_rejecting_a_revision_without_a_note_is_refused()
    {
        var revision = Given();

        await Should.ThrowAsync<ValidationException>(() => RejectAsync(null));

        revision.ReviewStatus.ShouldBe(RevisionStatus.Pending);
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(RevisionStatus.Approved)]
    [InlineData(RevisionStatus.Rejected)]
    public async Task A_revision_that_has_already_been_decided_is_refused(RevisionStatus already)
    {
        var revision = Given();
        revision.ReviewStatus = already;

        await Should.ThrowAsync<BusinessRuleException>(() => ApproveAsync());
        await Should.ThrowAsync<BusinessRuleException>(() => RejectAsync("no"));
    }

    [Fact]
    public async Task Rule_71_a_decision_drops_the_properties_cache()
    {
        Given();

        await ApproveAsync();

        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    [Fact]
    public async Task Rule_70_the_owner_is_emailed_the_decision()
    {
        var revision = Given(RevisionKind.Availability);

        await ApproveAsync();

        await _email.Received(1).SendRevisionDecisionAsync(
            revision.Property.Owner,
            revision.Property.Title,
            RevisionKind.Availability,
            "Approved",
            null);
    }

    /// <summary>
    /// The row goes before the file. A file deleted ahead of a save that then fails
    /// leaves a listing pointing at a photo that no longer loads, which is the one
    /// failure a visitor would actually see.
    /// </summary>
    [Fact]
    public async Task The_stored_files_are_deleted_only_after_the_rows_are_saved()
    {
        Given(photos:
        [
            TestData.Photo(1, PropertyId, isPrimary: true, sortOrder: 1),
            TestData.Photo(2, PropertyId, sortOrder: 2, status: PhotoStatus.PendingRemove)
        ]);

        var order = new List<string>();
        _unitOfWork.SaveChangesAsync().Returns(_ => { order.Add("save"); return 1; });
        _storage.DeleteAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(_ => { order.Add("blob"); return Task.CompletedTask; });

        await ApproveAsync();

        order.ShouldBe(["save", "blob"]);
    }

    [Fact]
    public async Task A_revision_that_does_not_exist_is_not_found()
    {
        _revisions.GetByIdAsync(RevisionId).Returns((PropertyRevision?)null);

        await Should.ThrowAsync<NotFoundException>(() => ApproveAsync());
    }
}
