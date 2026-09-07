using FluentValidation.TestHelper;
using RoomRaah.Application.Features.AdminUsers.Queries;
using RoomRaah.Application.Features.Inspections.Commands;
using RoomRaah.Application.Features.Inspections.Queries;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

public class InspectionValidatorTests
{
    private readonly RecordInspectionCommandValidator _record = new();
    private readonly GetDueForInspectionQueryValidator _due = new();
    private readonly GetAdminUsersQueryValidator _users = new();

    private static RecordInspectionCommand Valid() => new()
    {
        PropertyId = 1,
        InspectedAt = DateTime.UtcNow.AddDays(-1),
        Result = "Passed",
        Notes = "Everything matched.",
        FeeAmount = 1500m
    };

    [Fact]
    public void A_recorded_visit_in_the_past_is_accepted()
    {
        _record.TestValidate(Valid()).IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// Rule 115. A future date here would be a scheduled visit, which does not exist
    /// anywhere in this product — including in a row somebody typed by hand.
    /// </summary>
    [Fact]
    public void A_visit_cannot_be_recorded_for_a_date_that_has_not_come()
    {
        var command = Valid();
        command.InspectedAt = DateTime.UtcNow.AddDays(3);

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// Nullable on the command on purpose: an absent DateTime binds to a real date, so
    /// a required field that can never be absent is not required at all.
    /// </summary>
    [Fact]
    public void A_missing_visit_date_is_refused_rather_than_read_as_year_one()
    {
        var command = Valid();
        command.InspectedAt = null;

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("passed")]
    [InlineData("Failed")]
    public void A_result_the_product_has_is_accepted(string result)
    {
        var command = Valid();
        command.Result = result;

        _record.TestValidate(command).IsValid.ShouldBeTrue();
    }

    /// <summary>
    /// "1" and "-1" are the cases Enum.TryParse accepts on its own — a number outside
    /// the enumeration is not a name, and this is the first phase where one could be
    /// written into an inspection row.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Maybe")]
    [InlineData("5")]
    [InlineData("-1")]
    public void A_result_the_product_does_not_have_is_refused(string result)
    {
        var command = Valid();
        command.Result = result;

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_negative_fee_is_refused()
    {
        var command = Valid();
        command.FeeAmount = -1m;

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_fee_collected_in_the_future_is_refused()
    {
        var command = Valid();
        command.FeeCollectedAt = DateTime.UtcNow.AddDays(2);

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Notes_past_the_limit_are_refused()
    {
        var command = Valid();
        command.Notes = new string('a', RecordInspectionCommandValidator.MaximumNotesLength + 1);

        _record.TestValidate(command).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 51)]
    public void An_impossible_worklist_page_is_refused(int page, int pageSize)
    {
        _due.TestValidate(new GetDueForInspectionQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();
    }

    // ---------------------------------------------------------- admin users

    [Theory]
    [InlineData("Seeker")]
    [InlineData("owner")]
    [InlineData("ADMIN")]
    public void A_role_the_product_has_is_accepted(string role)
    {
        _users.TestValidate(new GetAdminUsersQuery { Role = role }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("3")]
    [InlineData("-1")]
    public void A_role_the_product_does_not_have_is_refused(string role)
    {
        _users.TestValidate(new GetAdminUsersQuery { Role = role }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void No_role_at_all_is_the_whole_list()
    {
        _users.TestValidate(new GetAdminUsersQuery()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("a")]
    public void A_search_term_that_is_too_short_is_refused(string q)
    {
        _users.TestValidate(new GetAdminUsersQuery { Q = q }).IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// The cap is 200 rather than the 60 the location autocomplete uses, because what
    /// an admin pastes in here is usually a whole email address — and §Field Validation
    /// allows one up to 200 characters, so a shorter cap refuses a search for an account
    /// that legitimately exists. The integration suite found this by being unable to
    /// look up its own test accounts.
    /// </summary>
    [Fact]
    public void A_whole_email_address_is_a_valid_search()
    {
        _users.TestValidate(new GetAdminUsersQuery
        {
            Q = "seeker.adminusers-find.7c9f1a2b3d4e5f60718293a4b5c6d7e8@roomraah.local"
        }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_search_term_past_two_hundred_characters_is_refused()
    {
        _users.TestValidate(new GetAdminUsersQuery { Q = new string('a', 201) })
            .IsValid.ShouldBeFalse();
    }
}
