using RoomRaah.Application.Inspections;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

/// <summary>
/// Rules 110 to 112, asserted where they are decided.
///
/// The same reasoning as <c>VisitLifecycle</c>: a rule asked inside a handler is tested
/// through whatever states that handler's tests happened to set up, and a rule asked
/// here is one a theory can walk over every value.
/// </summary>
public class BadgeRulesTests
{
    // ------------------------------------------------------------- granting

    /// <summary>
    /// Every combination of "does it already have one" and "what did the last visit
    /// say", including the listing nobody has ever visited.
    /// </summary>
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, InspectionResult.Passed, true)]
    [InlineData(false, InspectionResult.Failed, false)]
    [InlineData(true, null, false)]
    [InlineData(true, InspectionResult.Passed, false)]
    [InlineData(true, InspectionResult.Failed, false)]
    public void The_badge_is_granted_only_after_a_pass_and_only_once(
        bool alreadyBadged, InspectionResult? latest, bool expected)
    {
        var property = TestData.Property(status: PropertyStatus.Published);
        property.HasInspectionBadge = alreadyBadged;

        BadgeRules.MayGrant(property, latest).ShouldBe(expected);
    }

    /// <summary>
    /// The refusals are told apart, because they mean different things to the admin
    /// reading them: one says come back after a visit, the other says look again.
    /// </summary>
    [Fact]
    public void A_listing_that_already_carries_the_badge_is_refused_by_name()
    {
        var property = TestData.Property(status: PropertyStatus.Published);
        property.HasInspectionBadge = true;

        Should.Throw<BusinessRuleException>(
                () => BadgeRules.RequireGrantable(property, InspectionResult.Passed))
            .Message.ShouldBe(InspectionMessages.BadgeAlreadyGranted);
    }

    [Fact]
    public void A_listing_whose_last_visit_failed_is_refused_by_name()
    {
        var property = TestData.Property(status: PropertyStatus.Published);

        Should.Throw<BusinessRuleException>(
                () => BadgeRules.RequireGrantable(property, InspectionResult.Failed))
            .Message.ShouldBe(InspectionMessages.NoPassedInspection);
    }

    [Fact]
    public void A_listing_nobody_has_visited_is_refused()
    {
        var property = TestData.Property(status: PropertyStatus.Published);

        Should.Throw<BusinessRuleException>(
            () => BadgeRules.RequireGrantable(property, null));
    }

    // ------------------------------------------------------------- removing

    [Fact]
    public void A_badge_that_is_not_there_cannot_be_removed()
    {
        var property = TestData.Property(status: PropertyStatus.Published);

        Should.Throw<BusinessRuleException>(() => BadgeRules.RequireRemovable(property))
            .Message.ShouldBe(InspectionMessages.BadgeNotGranted);
    }

    [Fact]
    public void A_badge_that_is_there_can_be_removed()
    {
        var property = TestData.Property(status: PropertyStatus.Published);
        property.HasInspectionBadge = true;

        Should.NotThrow(() => BadgeRules.RequireRemovable(property));
    }

    // ----------------------------------------------------------- who may act

    /// <summary>
    /// Rules 109 and 110. The seeder has one admin of each kind on purpose, because
    /// "admin" and "admin who makes visits" are different answers and the product only
    /// notices the difference if somebody asks.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin, true, true)]
    [InlineData(UserRole.Admin, false, false)]
    [InlineData(UserRole.Owner, true, false)]
    [InlineData(UserRole.Seeker, true, false)]
    public void Only_an_admin_who_makes_visits_may_inspect(
        UserRole role, bool canInspect, bool expected)
    {
        var user = TestData.User(1, "person@roomraah.local", role);

        user.AdminProfile = new Domain.Entities.AdminProfile { CanInspect = canInspect };

        BadgeRules.CanInspect(user).ShouldBe(expected);
    }

    [Fact]
    public void An_admin_with_no_profile_at_all_is_not_an_inspector()
    {
        var user = TestData.User(1, "admin@roomraah.local", UserRole.Admin);

        BadgeRules.CanInspect(user).ShouldBeFalse();

        Should.Throw<UnauthorizedAccessException>(() => BadgeRules.RequireInspector(user));
    }

    // --------------------------------------------------------------- reason

    /// <summary>
    /// Rule 112, and a 400 rather than a 422 for the same reason rule 26's note is: an
    /// absent field is a rule about the shape of a request, not the state of a listing.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_badge_change_with_no_reason_is_a_bad_request(string? reason)
    {
        Should.Throw<FluentValidation.ValidationException>(
            () => BadgeRules.RequireReason(reason));
    }

    [Fact]
    public void A_reason_is_trimmed_rather_than_stored_with_its_whitespace()
    {
        BadgeRules.RequireReason("  Passed the visit.  ").ShouldBe("Passed the visit.");
    }

    // ------------------------------------------------------------- worklist

    [Fact]
    public void The_worklist_counts_back_thirty_days()
    {
        var now = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);

        BadgeRules.DueCutoff(now).ShouldBe(now.AddDays(-30));
    }
}
