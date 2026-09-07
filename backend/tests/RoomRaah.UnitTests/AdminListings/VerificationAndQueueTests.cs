using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminListings.Commands;
using RoomRaah.Application.Features.AdminListings.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.AdminListings;

public class RecordVerificationCheckCommandHandlerTests
{
    private const int AdminId = 3;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IVerificationCheckRepository _checks = Substitute.For<IVerificationCheckRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordVerificationCheckCommandHandler _handler;

    private readonly List<VerificationCheck> _created = [];

    public RecordVerificationCheckCommandHandlerTests()
    {
        _checks.CreateAsync(Arg.Do<VerificationCheck>(_created.Add)).Returns(Task.CompletedTask);

        _handler = new RecordVerificationCheckCommandHandler(
            _properties, _checks, _cache, _unitOfWork, TestMapper.Create());
    }

    private Property Given(bool checksPass = false)
    {
        var property = TestData.ReviewableProperty(PropertyId, 7, PropertyStatus.PendingReview, checksPass);
        _properties.GetForReviewAsync(PropertyId).Returns(property);
        return property;
    }

    private Task<List<VerificationCheckDto>> HandleAsync(
        VerificationCheckType type = VerificationCheckType.Address,
        VerificationStatus status = VerificationStatus.Checked,
        string? note = null) =>
        _handler.Handle(new RecordVerificationCheckCommand
        {
            PropertyId = PropertyId,
            AdminId = AdminId,
            CheckType = type.ToString(),
            Status = status.ToString(),
            Note = note,
            EvidenceDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc)
        }, CancellationToken.None);

    [Fact]
    public async Task A_check_that_has_not_been_recorded_before_is_created()
    {
        var property = Given();

        var result = await HandleAsync(note: "  Utility bill matches.  ");

        _created.ShouldHaveSingleItem();
        property.VerificationChecks.ShouldHaveSingleItem();
        result.ShouldHaveSingleItem().Status.ShouldBe("Checked");

        var check = property.VerificationChecks.Single();
        check.ReviewedByAdminId.ShouldBe(AdminId);
        check.Note.ShouldBe("Utility bill matches.");
        check.EvidenceDate.ShouldBe(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// Rule 64. One row per listing per type: an admin who was wrong should be able
    /// to correct themselves, and rule 23 has to be able to ask each check a single
    /// question rather than reconcile a stack of opinions.
    /// </summary>
    [Fact]
    public async Task Rule_64_recording_the_same_check_again_replaces_the_verdict()
    {
        var property = Given();

        await HandleAsync(VerificationCheckType.Address, VerificationStatus.Failed);
        await HandleAsync(VerificationCheckType.Address, VerificationStatus.Checked);

        property.VerificationChecks.ShouldHaveSingleItem().Status.ShouldBe(VerificationStatus.Checked);
        _created.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_different_check_type_is_a_different_row()
    {
        var property = Given();

        await HandleAsync(VerificationCheckType.Address);
        await HandleAsync(VerificationCheckType.Photos);

        property.VerificationChecks.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Rule_71_recording_a_check_drops_the_properties_cache()
    {
        Given();

        await HandleAsync();

        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetForReviewAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync());
    }
}

public class RecordVerificationCheckCommandValidatorTests
{
    private readonly RecordVerificationCheckCommandValidator _validator = new();

    private static RecordVerificationCheckCommand Valid() => new()
    {
        CheckType = "Address",
        Status = "Checked"
    };

    [Fact]
    public void A_well_formed_check_passes()
    {
        _validator.Validate(Valid()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Weather")]
    public void A_check_type_that_is_not_one_of_the_five_is_refused(string type)
    {
        var command = Valid();
        command.CheckType = type;

        _validator.Validate(command).Errors.ShouldContain(e => e.PropertyName == "CheckType");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Maybe")]
    public void A_status_that_is_not_one_of_the_three_is_refused(string status)
    {
        var command = Valid();
        command.Status = status;

        _validator.Validate(command).Errors.ShouldContain(e => e.PropertyName == "Status");
    }

    [Fact]
    public void A_note_longer_than_a_thousand_characters_is_refused()
    {
        var command = Valid();
        command.Note = new string('x', 1001);

        _validator.Validate(command).Errors.ShouldContain(e => e.PropertyName == "Note");
    }
}

public class GetRevisionQueueQueryHandlerTests
{
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly GetRevisionQueueQueryHandler _handler;

    public GetRevisionQueueQueryHandlerTests()
    {
        _handler = new GetRevisionQueueQueryHandler(_revisions);
    }

    private PropertyRevision Pending(int id, RevisionKind kind, Action<PropertyRevision>? change = null)
    {
        var property = TestData.ReviewableProperty(id, 7, PropertyStatus.Published);
        var revision = TestData.Revision(property, kind, change);
        revision.Id = id;
        return revision;
    }

    private Task<PagedResult<RevisionQueueRowDto>> HandleAsync(string? kind = null) =>
        _handler.Handle(new GetRevisionQueueQuery { Kind = kind, Page = 1, PageSize = 12 },
            CancellationToken.None);

    /// <summary>
    /// An availability row is worked in a batch without being opened, so the bed
    /// count now, the bed count asked for and the total all travel on the row.
    /// </summary>
    [Fact]
    public async Task An_availability_row_carries_enough_to_be_approved_without_opening_it()
    {
        var revision = Pending(1, RevisionKind.Availability, r => r.AvailableBeds = 0);
        _revisions.GetQueueAsync(RevisionKind.Availability, 1, 12).Returns([revision]);
        _revisions.CountQueueAsync(RevisionKind.Availability).Returns(1);

        var page = await HandleAsync("Availability");

        var row = page.Items.ShouldHaveSingleItem();

        row.CurrentAvailableBeds.ShouldBe(2);
        row.ProposedAvailableBeds.ShouldBe(0);
        row.TotalBeds.ShouldBe(4);
        row.ChangedFields.ShouldBe(["AvailableBeds"]);
        row.PropertyTitle.ShouldNotBeNullOrWhiteSpace();
        row.CityName.ShouldBe("Lahore");
    }

    /// <summary>
    /// A content row says which fields moved rather than their values: it is what an
    /// admin decides whether to open the diff for.
    /// </summary>
    [Fact]
    public async Task A_content_row_names_the_fields_that_moved()
    {
        var revision = Pending(2, RevisionKind.Content, r =>
        {
            r.Title = "Renamed";
            r.MonthlyRent = 21000m;
        });

        _revisions.GetQueueAsync(null, 1, 12).Returns([revision]);
        _revisions.CountQueueAsync(null).Returns(1);

        var page = await HandleAsync();

        page.Items.ShouldHaveSingleItem().ChangedFields
            .ShouldBe(["Title", "MonthlyRent"], ignoreOrder: true);
    }

    [Fact]
    public async Task The_page_carries_the_total_across_every_page()
    {
        _revisions.GetQueueAsync(null, 1, 12).Returns([]);
        _revisions.CountQueueAsync(null).Returns(97);

        var page = await HandleAsync();

        page.TotalCount.ShouldBe(97);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(12);
    }
}

public class ReviewQueueValidatorTests
{
    [Theory]
    [InlineData("Content", true)]
    [InlineData("availability", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("Everything", false)]
    public void A_revision_queue_takes_a_kind_it_recognises_or_none(string? kind, bool valid)
    {
        new GetRevisionQueueQueryValidator()
            .Validate(new GetRevisionQueueQuery { Kind = kind })
            .IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("PendingReview", true)]
    [InlineData("published", true)]
    [InlineData(null, true)]
    [InlineData("Sold", false)]
    public void A_listing_queue_takes_a_status_it_recognises_or_none(string? status, bool valid)
    {
        new GetListingQueueQueryValidator()
            .Validate(new GetListingQueueQuery { Status = status })
            .IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(0, 12, false)]
    [InlineData(1, 51, false)]
    [InlineData(1, 50, true)]
    public void The_queue_pages_the_same_way_every_other_list_does(int page, int size, bool valid)
    {
        new GetListingQueueQueryValidator()
            .Validate(new GetListingQueueQuery { Page = page, PageSize = size })
            .IsValid.ShouldBe(valid);
    }
}
