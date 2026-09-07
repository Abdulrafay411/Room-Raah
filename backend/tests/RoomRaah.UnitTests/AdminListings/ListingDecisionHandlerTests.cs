using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminListings.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.AdminListings;

public class ListingDecisionHandlerTests
{
    private const int AdminId = 3;
    private const int OwnerId = 7;
    private const int PropertyId = 11;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly ApproveListingCommandHandler _approve;
    private readonly RejectListingCommandHandler _reject;
    private readonly RequestListingChangesCommandHandler _requestChanges;

    public ListingDecisionHandlerTests()
    {
        var mapper = TestMapper.Create();

        _approve = new ApproveListingCommandHandler(
            _properties, _cache, _email, _unitOfWork, mapper,
            NullLogger<ApproveListingCommandHandler>.Instance);

        _reject = new RejectListingCommandHandler(
            _properties, _cache, _email, _unitOfWork, mapper,
            NullLogger<RejectListingCommandHandler>.Instance);

        _requestChanges = new RequestListingChangesCommandHandler(
            _properties, _cache, _email, _unitOfWork, mapper,
            NullLogger<RequestListingChangesCommandHandler>.Instance);
    }

    private Property Given(
        PropertyStatus status = PropertyStatus.PendingReview,
        bool checksPass = true)
    {
        var property = TestData.ReviewableProperty(PropertyId, OwnerId, status, checksPass);
        _properties.GetForReviewAsync(PropertyId).Returns(property);
        return property;
    }

    private Task<AdminPropertyDto> ApproveAsync(string? note = null) =>
        _approve.Handle(
            new ApproveListingCommand { PropertyId = PropertyId, AdminId = AdminId, AdminNote = note },
            CancellationToken.None);

    private Task<AdminPropertyDto> RejectAsync(string? note) =>
        _reject.Handle(
            new RejectListingCommand { PropertyId = PropertyId, AdminId = AdminId, AdminNote = note },
            CancellationToken.None);

    private Task<AdminPropertyDto> RequestChangesAsync(string? note) =>
        _requestChanges.Handle(
            new RequestListingChangesCommand { PropertyId = PropertyId, AdminId = AdminId, AdminNote = note },
            CancellationToken.None);

    [Fact]
    public async Task Rule_24_approving_publishes_the_listing_and_starts_its_freshness_clock()
    {
        var property = Given();
        property.AvailabilityConfirmedAt = DateTime.UtcNow.AddDays(-40);

        var result = await ApproveAsync();

        property.Status.ShouldBe(PropertyStatus.Published);
        property.AvailabilityConfirmedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
        result.Status.ShouldBe("Published");
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_23_a_listing_with_an_unfinished_check_cannot_be_published()
    {
        var property = Given(checksPass: false);
        property.VerificationChecks.Add(
            TestData.Check(PropertyId, VerificationCheckType.OwnerIdentity));

        await Should.ThrowAsync<BusinessRuleException>(() => ApproveAsync());

        property.Status.ShouldBe(PropertyStatus.PendingReview);
        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Rule_23_a_failed_check_is_not_a_passed_one()
    {
        var property = Given(checksPass: false);

        foreach (var type in Application.Listings.ListingReview.RequiredToPublish)
        {
            property.VerificationChecks.Add(TestData.Check(PropertyId, type));
        }

        property.VerificationChecks.First().Status = VerificationStatus.Failed;

        await Should.ThrowAsync<BusinessRuleException>(() => ApproveAsync());
    }

    /// <summary>
    /// Rule 23 gates publishing, and availability is deliberately not one of the four:
    /// a bed count is confirmed continuously afterwards, and waiting on it would stop
    /// a correct listing going live.
    /// </summary>
    [Fact]
    public async Task Rule_23_availability_is_not_one_of_the_four_gates()
    {
        var property = Given();
        property.VerificationChecks
            .Any(c => c.CheckType == VerificationCheckType.Availability)
            .ShouldBeFalse();

        await ApproveAsync();

        property.Status.ShouldBe(PropertyStatus.Published);
    }

    [Fact]
    public async Task Rule_26_a_rejection_without_a_note_is_refused()
    {
        var property = Given();

        await Should.ThrowAsync<ValidationException>(() => RejectAsync(null));
        await Should.ThrowAsync<ValidationException>(() => RejectAsync("   "));

        property.Status.ShouldBe(PropertyStatus.PendingReview);
    }

    [Fact]
    public async Task Rule_63_a_request_for_changes_without_a_note_is_refused()
    {
        Given();

        await Should.ThrowAsync<ValidationException>(() => RequestChangesAsync(null));
    }

    [Fact]
    public async Task A_rejection_leaves_the_listing_rejected()
    {
        var property = Given();

        await RejectAsync("The address does not match the utility bill.");

        property.Status.ShouldBe(PropertyStatus.Rejected);
    }

    [Fact]
    public async Task A_request_for_changes_sends_the_listing_back_to_its_owner()
    {
        var property = Given();

        await RequestChangesAsync("Please add a photo of the kitchen.");

        property.Status.ShouldBe(PropertyStatus.ChangesRequested);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Published)]
    [InlineData(PropertyStatus.Rejected)]
    [InlineData(PropertyStatus.ChangesRequested)]
    public async Task Rule_62_only_a_listing_waiting_for_review_can_be_decided(PropertyStatus status)
    {
        Given(status);

        await Should.ThrowAsync<BusinessRuleException>(() => ApproveAsync());
        await Should.ThrowAsync<BusinessRuleException>(() => RejectAsync("no"));
        await Should.ThrowAsync<BusinessRuleException>(() => RequestChangesAsync("no"));
    }

    /// <summary>
    /// Rule 124, which is where `Unpublished` stopped being like the other four.
    ///
    /// Until Phase 7 nothing in the product could set that status at all, so it was in
    /// this theory alongside Draft and Rejected. Now a listing an admin took off the
    /// site comes back through approve — the same decision it was the first time, with
    /// rule 23's four checks still in front of it.
    /// </summary>
    [Fact]
    public async Task Rule_124_an_unpublished_listing_comes_back_through_approve()
    {
        var property = Given(PropertyStatus.Unpublished);

        await ApproveAsync();

        property.Status.ShouldBe(PropertyStatus.Published);
    }

    [Fact]
    public async Task Rule_124_an_unpublished_listing_still_has_to_pass_the_four_checks()
    {
        Given(PropertyStatus.Unpublished, checksPass: false);

        await Should.ThrowAsync<BusinessRuleException>(() => ApproveAsync());
    }

    /// <summary>
    /// Only approve gained the new starting status. Rejecting or asking for changes on
    /// a listing that is already off the site is not a decision anybody needs to make.
    /// </summary>
    [Fact]
    public async Task Rule_124_does_not_widen_the_other_two_decisions()
    {
        Given(PropertyStatus.Unpublished);

        await Should.ThrowAsync<BusinessRuleException>(() => RejectAsync("no"));
        await Should.ThrowAsync<BusinessRuleException>(() => RequestChangesAsync("no"));
    }

    [Fact]
    public async Task Rule_70_the_owner_is_emailed_the_decision_and_the_note()
    {
        var property = Given();

        await RejectAsync("  The address does not match the utility bill.  ");

        await _email.Received(1).SendListingDecisionAsync(
            property.Owner,
            property.Title,
            "Rejected",
            "The address does not match the utility bill.");
    }

    [Fact]
    public async Task Rule_71_every_decision_drops_the_properties_cache()
    {
        Given();

        await ApproveAsync();

        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    /// <summary>
    /// Rule 33 has no exception for an admin panel. The destination type carries no
    /// email and no phone number at all, which is what makes this true by shape
    /// rather than by anybody remembering to ignore two members.
    /// </summary>
    [Fact]
    public async Task Rule_33_the_admin_view_carries_no_way_to_contact_the_owner()
    {
        Given();

        var result = await ApproveAsync();

        result.Owner.FullName.ShouldBe("Test Person");
        result.Owner.IdentityStatus.ShouldBe("Checked");

        typeof(ListingOwnerSummaryDto).GetProperties()
            .Select(p => p.Name)
            .ShouldNotContain(name => name.Contains("Email") || name.Contains("Phone"));
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        _properties.GetForReviewAsync(PropertyId).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(() => ApproveAsync());
    }
}
