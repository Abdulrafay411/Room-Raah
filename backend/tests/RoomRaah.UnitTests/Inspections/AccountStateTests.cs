using RoomRaah.Application.Inspections;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

/// <summary>
/// Rules 118 and 121 — the two ways an admin panel locks everybody out of itself, and
/// neither is obvious from inside the handler that does the switching.
/// </summary>
public class AccountStateTests
{
    private const int ActingAdmin = 1;

    [Fact]
    public void An_admin_cannot_switch_off_their_own_account()
    {
        var self = TestData.User(ActingAdmin, "admin@roomraah.local", UserRole.Admin);

        Should.Throw<BusinessRuleException>(
                () => AccountState.RequireChangeable(self, ActingAdmin))
            .Message.ShouldBe(InspectionMessages.CannotDeactivateSelf);
    }

    /// <summary>
    /// The second protection, which the first does not give: without it two admins can
    /// switch each other off and the panel ends up with nobody in it.
    /// </summary>
    [Fact]
    public void An_admin_cannot_switch_off_another_admin()
    {
        var other = TestData.User(2, "admin2@roomraah.local", UserRole.Admin);

        Should.Throw<UnauthorizedAccessException>(
                () => AccountState.RequireChangeable(other, ActingAdmin))
            .Message.ShouldBe(InspectionMessages.CannotChangeAnotherAdmin);
    }

    [Theory]
    [InlineData(UserRole.Seeker)]
    [InlineData(UserRole.Owner)]
    public void Any_other_account_may_be_switched(UserRole role)
    {
        var user = TestData.User(2, "person@roomraah.local", role);

        Should.NotThrow(() => AccountState.RequireChangeable(user, ActingAdmin));
    }

    /// <summary>
    /// Rule 121. Refused rather than shrugged off: unlike marking a conversation read,
    /// this is not something a client does twice by accident — a second deactivation
    /// means somebody is acting on a stale view of the account.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Switching_an_account_to_the_state_it_is_already_in_is_refused(
        bool currentlyActive, bool requested)
    {
        var user = TestData.User(2, "person@roomraah.local", UserRole.Seeker, isActive: currentlyActive);

        Should.Throw<BusinessRuleException>(
            () => AccountState.RequireNotAlready(user, requested));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Switching_an_account_to_the_other_state_is_allowed(
        bool currentlyActive, bool requested)
    {
        var user = TestData.User(2, "person@roomraah.local", UserRole.Seeker, isActive: currentlyActive);

        Should.NotThrow(() => AccountState.RequireNotAlready(user, requested));
    }

    /// <summary>Rule 119.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_change_with_no_reason_is_a_bad_request(string? reason)
    {
        Should.Throw<FluentValidation.ValidationException>(
            () => AccountState.RequireReason(reason));
    }
}
