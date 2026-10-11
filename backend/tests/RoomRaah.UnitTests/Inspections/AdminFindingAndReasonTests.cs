using AutoMapper;
using FluentValidation.TestHelper;
using NSubstitute;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminListings.Queries;
using RoomRaah.Application.Features.AdminUsers.Commands;
using RoomRaah.Application.Features.Inspections.Queries;
using RoomRaah.Application.Inspections;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

/// <summary>Rules 135 to 138.</summary>
public class AdminFindingAndReasonTests
{
    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly IMapper _mapper = TestMapper.Create();

    // --------------------------------------------------------------- rule 135

    [Theory]
    [InlineData("a")]
    [InlineData(null)]
    public void Rule_135_a_search_of_one_character_is_refused_and_none_is_fine(string? q)
    {
        var result = new GetListingQueueQueryValidator().TestValidate(new GetListingQueueQuery { Q = q });

        if (q is null)
        {
            result.ShouldNotHaveAnyValidationErrors();
        }
        else
        {
            result.ShouldHaveValidationErrorFor(x => x.Q);
        }
    }

    [Fact]
    public void Rule_135_a_search_longer_than_any_title_is_refused()
    {
        new GetListingQueueQueryValidator()
            .TestValidate(new GetListingQueueQuery { Q = new string('x', 151) })
            .ShouldHaveValidationErrorFor(x => x.Q);
    }

    [Fact]
    public async Task Rule_135_the_search_is_trimmed_and_reaches_the_repository()
    {
        _properties.GetByStatusAsync(PropertyStatus.Published, "Lahore", 1, 12).Returns([]);

        await new GetListingQueueQueryHandler(_properties, _mapper).Handle(
            new GetListingQueueQuery { Status = "Published", Q = "  Lahore  " }, default);

        await _properties.Received(1).GetByStatusAsync(PropertyStatus.Published, "Lahore", 1, 12);
        await _properties.Received(1).CountByStatusAsync(PropertyStatus.Published, "Lahore");
    }

    [Fact]
    public async Task Rule_135_a_blank_search_is_no_search()
    {
        _properties.GetByStatusAsync(null, null, 1, 12).Returns([]);

        await new GetListingQueueQueryHandler(_properties, _mapper).Handle(
            new GetListingQueueQuery { Q = "   " }, default);

        await _properties.Received(1).GetByStatusAsync(null, null, 1, 12);
    }

    [Fact]
    public void Rule_135_a_queue_row_says_whether_the_listing_is_badged()
    {
        var property = TestData.ReviewableProperty(1, 7, PropertyStatus.Published);
        property.HasInspectionBadge = true;

        _mapper.Map<AdminPropertySummaryDto>(property).HasInspectionBadge.ShouldBeTrue();
    }

    // --------------------------------------------------------------- rule 136

    [Fact]
    public async Task Rule_136_requested_listings_come_back_with_when_the_owner_asked()
    {
        var asked = DateTime.UtcNow.AddDays(-3);
        var property = TestData.ReviewableProperty(5, 7, PropertyStatus.Published);
        property.InspectionRequestedAt = asked;
        _properties.GetInspectionRequestedAsync(1, 12).Returns([property]);
        _properties.CountInspectionRequestedAsync().Returns(1);

        var page = await new GetRequestedInspectionsQueryHandler(_properties, _mapper)
            .Handle(new GetRequestedInspectionsQuery(), default);

        page.TotalCount.ShouldBe(1);
        var row = page.Items.ShouldHaveSingleItem();
        row.PropertyId.ShouldBe(5);
        row.InspectionRequestedAt.ShouldBe(asked);
        row.OwnerDisplayName.ShouldBe(property.Owner.FullName);
        row.CityName.ShouldBe("Lahore");
    }

    [Fact]
    public void Rule_136_the_owner_is_named_and_nothing_else_about_them()
    {
        var names = typeof(RequestedInspectionDto).GetProperties().Select(p => p.Name).ToList();

        names.ShouldContain(nameof(RequestedInspectionDto.OwnerDisplayName));
        names.ShouldNotContain("OwnerEmail");
        names.ShouldNotContain("OwnerPhone");
        names.ShouldNotContain("OwnerId");
    }

    // --------------------------------------------------------------- rule 137

    [Fact]
    public void Rule_137_the_message_carries_the_reason()
    {
        AuthMessages.AccountDeactivatedBecause("Repeated abusive messages.")
            .ShouldBe("This account has been deactivated: Repeated abusive messages.");
    }

    [Fact]
    public void Rule_137_an_account_deactivated_before_reasons_were_kept_gets_the_old_message()
    {
        AuthMessages.AccountDeactivatedBecause(null).ShouldBe(AuthMessages.AccountDeactivated);
    }

    [Fact]
    public void Rule_137_a_reason_longer_than_can_be_stored_is_refused()
    {
        new SetAccountActiveCommandValidator()
            .TestValidate(new SetAccountActiveCommand { Reason = new string('x', 1001) })
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void Rule_137_a_missing_reason_is_left_to_the_handler_and_rule_119()
    {
        new SetAccountActiveCommandValidator()
            .TestValidate(new SetAccountActiveCommand { Reason = null })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rule_137_the_admin_detail_carries_the_stored_reason()
    {
        var user = TestData.User(2, isActive: false);
        user.DeactivationReason = "Repeated abusive messages.";

        _mapper.Map<AdminUserDto>(user).DeactivationReason.ShouldBe("Repeated abusive messages.");
    }

    // --------------------------------------------------------------- rule 138

    private static BadgeEvent Event(int id, BadgeAction action, string reason, int daysAgo) => new()
    {
        Id = id,
        PropertyId = 1,
        Action = action,
        Reason = reason,
        ActedByAdminId = 9,
        ActedAt = DateTime.UtcNow.AddDays(-daysAgo)
    };

    [Fact]
    public void Rule_138_a_removed_badge_tells_the_owner_when_and_why()
    {
        var property = TestData.Property(1, 7, PropertyStatus.Published);
        property.BadgeEvents.Add(Event(1, BadgeAction.Granted, "Passed inspection", 20));
        property.BadgeEvents.Add(Event(2, BadgeAction.Removed, "Upheld report: no hot water", 2));

        var dto = _mapper.Map<OwnerPropertySummaryDto>(property);

        dto.BadgeRemovedReason.ShouldBe("Upheld report: no hot water");
        dto.BadgeRemovedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Rule_138_a_badge_granted_again_has_nothing_left_to_explain()
    {
        var property = TestData.Property(1, 7, PropertyStatus.Published);
        property.HasInspectionBadge = true;
        property.BadgeEvents.Add(Event(1, BadgeAction.Removed, "Upheld report", 10));
        property.BadgeEvents.Add(Event(2, BadgeAction.Granted, "Re-inspected and passed", 1));

        var dto = _mapper.Map<OwnerPropertySummaryDto>(property);

        dto.BadgeRemovedReason.ShouldBeNull();
        dto.BadgeRemovedAt.ShouldBeNull();
    }

    [Fact]
    public void Rule_138_a_badge_that_was_never_removed_says_nothing()
    {
        var property = TestData.Property(1, 7, PropertyStatus.Published);

        BadgeRules.CurrentRemoval(property).ShouldBeNull();
        _mapper.Map<OwnerPropertySummaryDto>(property).BadgeRemovedReason.ShouldBeNull();
    }
}
