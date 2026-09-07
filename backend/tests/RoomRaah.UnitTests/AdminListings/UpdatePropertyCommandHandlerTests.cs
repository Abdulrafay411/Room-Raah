using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.AdminListings;

public class UpdatePropertyCommandHandlerTests
{
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IPropertyRevisionRepository _revisions = Substitute.For<IPropertyRevisionRepository>();
    private readonly ILocationRepository _locations = Substitute.For<ILocationRepository>();
    private readonly IFacilityRepository _facilities = Substitute.For<IFacilityRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UpdatePropertyCommandHandler _handler;

    private readonly List<PropertyRevision> _created = [];

    public UpdatePropertyCommandHandlerTests()
    {
        _locations.AreaExistsAsync(Arg.Any<int>()).Returns(true);
        _facilities.GetByIdsAsync(Arg.Any<List<int>>())
            .Returns(call => call.Arg<List<int>>().Select(id => TestData.Facility(id)).ToList());
        _revisions.CreateAsync(Arg.Do<PropertyRevision>(_created.Add)).Returns(Task.CompletedTask);

        _handler = new UpdatePropertyCommandHandler(
            _properties, _revisions, _locations, _facilities, _cache, _unitOfWork, TestMapper.Create());
    }

    private Property Given(PropertyStatus status = PropertyStatus.Draft, int ownerId = OwnerId)
    {
        var property = TestData.Property(PropertyId, ownerId, status, TestData.Photos(3, PropertyId));
        _properties.GetByIdAsync(PropertyId).Returns(property);
        return property;
    }

    /// <summary>The listing exactly as it stands, so a test changes only what it names.</summary>
    private static UpdatePropertyCommand Unchanged(Property property) => new()
    {
        PropertyId = PropertyId,
        OwnerId = OwnerId,
        AreaId = property.AreaId,
        Title = property.Title,
        Description = property.Description,
        AddressLine = property.AddressLine,
        Latitude = property.Latitude,
        Longitude = property.Longitude,
        RoomType = property.RoomType.ToString(),
        GenderPolicy = property.GenderPolicy.ToString(),
        TotalBeds = property.TotalBeds,
        AvailableBeds = property.AvailableBeds,
        MonthlyRent = property.MonthlyRent,
        SecurityDeposit = property.SecurityDeposit,
        UtilitiesCharge = property.UtilitiesCharge,
        MessCharge = property.MessCharge,
        HouseRules = property.HouseRules,
        FacilityIds = property.Facilities.Select(f => f.FacilityId).ToList()
    };

    private Task<OwnerListingUpdateDto> HandleAsync(UpdatePropertyCommand command) =>
        _handler.Handle(command, CancellationToken.None);

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.ChangesRequested)]
    [InlineData(PropertyStatus.Rejected)]
    [InlineData(PropertyStatus.Unpublished)]
    public async Task Rule_60_a_listing_the_public_cannot_see_is_edited_in_place(PropertyStatus status)
    {
        var property = Given(status);

        var command = Unchanged(property);
        command.Title = "A quieter room on the same street";

        var result = await HandleAsync(command);

        property.Title.ShouldBe("A quieter room on the same street");
        result.PendingRevision.ShouldBeNull();
        _created.ShouldBeEmpty();
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_60_a_listing_under_review_refuses_the_edit()
    {
        var property = Given(PropertyStatus.PendingReview);

        var command = Unchanged(property);
        command.Title = "Changed while an admin is reading it";

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(command));

        property.Title.ShouldNotBe("Changed while an admin is reading it");
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_19_a_published_listing_keeps_serving_the_approved_version()
    {
        var property = Given(PropertyStatus.Published);

        var command = Unchanged(property);
        command.MonthlyRent = 21000m;

        var result = await HandleAsync(command);

        property.MonthlyRent.ShouldBe(18000m);
        _created.ShouldHaveSingleItem().MonthlyRent.ShouldBe(21000m);
        result.PendingRevision.ShouldNotBeNull().Changes
            .ShouldContain(c => c.Field == "MonthlyRent" && c.Proposed == "21000");
    }

    [Fact]
    public async Task Rule_21_a_bed_count_on_its_own_is_an_availability_revision()
    {
        var property = Given(PropertyStatus.Published);

        var command = Unchanged(property);
        command.AvailableBeds = 1;

        await HandleAsync(command);

        _created.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Availability);
    }

    [Fact]
    public async Task Rule_21_a_bed_count_alongside_anything_else_is_a_content_revision()
    {
        var property = Given(PropertyStatus.Published);

        var command = Unchanged(property);
        command.AvailableBeds = 1;
        command.MonthlyRent = 21000m;

        await HandleAsync(command);

        _created.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Content);
    }

    [Fact]
    public async Task Rule_61_a_revision_that_proposes_nothing_is_refused()
    {
        var property = Given(PropertyStatus.Published);

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(Unchanged(property)));

        _created.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rule_20_a_second_revision_of_the_same_kind_is_refused()
    {
        var property = Given(PropertyStatus.Published);
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Content)
            .Returns(TestData.Revision(property));

        var command = Unchanged(property);
        command.Title = "Another change while the first waits";

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(command));
    }

    /// <summary>
    /// Rule 20 allows one of each kind, so an open content revision does not block
    /// an owner from confirming how many beds are free.
    /// </summary>
    [Fact]
    public async Task Rule_20_a_revision_of_the_other_kind_does_not_block_it()
    {
        var property = Given(PropertyStatus.Published);
        _revisions.GetPendingAsync(PropertyId, RevisionKind.Content)
            .Returns(TestData.Revision(property));

        var command = Unchanged(property);
        command.AvailableBeds = 0;

        await HandleAsync(command);

        _created.ShouldHaveSingleItem().Kind.ShouldBe(RevisionKind.Availability);
    }

    [Fact]
    public async Task Rule_53_an_area_that_does_not_exist_is_refused()
    {
        var property = Given();
        _locations.AreaExistsAsync(99).Returns(false);

        var command = Unchanged(property);
        command.AreaId = 99;

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(command));
    }

    [Fact]
    public async Task Rule_53_a_facility_that_does_not_exist_is_refused()
    {
        var property = Given();
        _facilities.GetByIdsAsync(Arg.Any<List<int>>()).Returns([]);

        var command = Unchanged(property);
        command.FacilityIds = [4, 5];

        await Should.ThrowAsync<BusinessRuleException>(() => HandleAsync(command));
    }

    [Fact]
    public async Task Rule_18_another_owners_listing_may_not_be_edited()
    {
        var property = Given(ownerId: OwnerId + 1);

        await Should.ThrowAsync<UnauthorizedAccessException>(() => HandleAsync(Unchanged(property)));
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        var property = TestData.Property(PropertyId, OwnerId);
        _properties.GetByIdAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => HandleAsync(Unchanged(property)));
    }

    /// <summary>
    /// A revision changes nothing the public can see, so there is nothing cached to
    /// drop. A direct edit does, even on a listing that is not published now, because
    /// it may have been published before.
    /// </summary>
    [Fact]
    public async Task A_revision_does_not_evict_the_cache_and_a_direct_edit_does()
    {
        var published = Given(PropertyStatus.Published);
        var proposing = Unchanged(published);
        proposing.Title = "Proposed";

        await HandleAsync(proposing);

        await _cache.DidNotReceive().RemoveByPrefixAsync(Arg.Any<string>());

        var draft = Given(PropertyStatus.Draft);
        var editing = Unchanged(draft);
        editing.Title = "Edited";

        await HandleAsync(editing);

        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }
}
