using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Reports.Commands;
using RoomRaah.Application.Features.Reports.Queries;
using RoomRaah.Application.Features.Reviews.Commands;
using RoomRaah.Application.Features.Reviews.Queries;
using RoomRaah.Application.Features.Saved.Queries;
using RoomRaah.Application.Features.Visits.Commands;
using RoomRaah.Application.Features.Visits.Queries;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

public class SeekerValidatorTests
{
    private readonly CreateVisitRequestCommandValidator _visit = new();
    private readonly RespondToVisitCommandValidator _respond = new();
    private readonly CreateReviewCommandValidator _review = new();
    private readonly ModerateReviewCommandValidator _moderate = new();
    private readonly CreateReportCommandValidator _report = new();
    private readonly ResolveReportCommandValidator _resolve = new();

    private static CreateVisitRequestCommand Visit(
        string type = "Physical", DateTime? at = null, int propertyId = 1) => new()
    {
        PropertyId = propertyId,
        VisitType = type,
        PreferredAt = at ?? DateTime.UtcNow.AddDays(3)
    };

    private static CreateReviewCommand Review(
        int rating = 4, string comment = "Good room.", string stay = "Visited") => new()
    {
        PropertyId = 1,
        Rating = rating,
        Comment = comment,
        StayStatus = stay
    };

    private static CreateReportCommand Report(
        string reason = "AlreadyTaken", string details = "The room has gone.") => new()
    {
        PropertyId = 1,
        Reason = reason,
        Details = details
    };

    // ------------------------------------------------------------------ visits

    [Fact]
    public void A_visit_in_the_future_of_a_known_type_is_accepted()
    {
        _visit.TestValidate(Visit()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_visit_in_the_past_is_refused()
    {
        _visit.TestValidate(Visit(at: DateTime.UtcNow.AddHours(-1)))
            .ShouldHaveValidationErrorFor(x => x.PreferredAt);
    }

    /// <summary>
    /// The far edge. Without it a request for the year 3000 sits at the top of an
    /// owner's list forever and can never be completed, because rule 90 waits for a
    /// date that never comes.
    /// </summary>
    [Fact]
    public void A_visit_further_ahead_than_sixty_days_is_refused()
    {
        _visit.TestValidate(Visit(at: DateTime.UtcNow.AddDays(
                CreateVisitRequestCommandValidator.MaximumDaysAhead + 1)))
            .ShouldHaveValidationErrorFor(x => x.PreferredAt);
    }

    /// <summary>
    /// "2" is the one worth having. Enum.TryParse accepts a number outside the
    /// enumeration and hands back a value nothing in the product matches, which
    /// this phase is the first that could have written into the database.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("Inspection")]
    [InlineData("2")]
    [InlineData("-1")]
    public void A_visit_type_the_product_does_not_have_is_refused(string type)
    {
        _visit.TestValidate(Visit(type: type)).ShouldHaveValidationErrorFor(x => x.VisitType);
    }

    [Theory]
    [InlineData("Physical")]
    [InlineData("video")]
    public void Both_visit_types_are_accepted_however_they_are_cased(string type)
    {
        _visit.TestValidate(Visit(type: type)).ShouldNotHaveValidationErrorFor(x => x.VisitType);
    }

    [Theory]
    [InlineData("Accepted")]
    [InlineData("declined")]
    public void An_answer_is_accepted_or_declined(string status)
    {
        _respond.TestValidate(new RespondToVisitCommand { Status = status })
            .ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    /// <summary>
    /// Rule 88 names two answers. Completing and cancelling have their own endpoints,
    /// open to people this one does not necessarily let in, so neither may be reached
    /// by naming it here.
    /// </summary>
    [Theory]
    [InlineData("Completed")]
    [InlineData("Cancelled")]
    [InlineData("Requested")]
    public void No_other_status_may_be_reached_through_the_respond_endpoint(string status)
    {
        _respond.TestValidate(new RespondToVisitCommand { Status = status })
            .ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void A_response_note_longer_than_a_thousand_characters_is_refused()
    {
        _respond.TestValidate(new RespondToVisitCommand
        {
            Status = "Accepted",
            OwnerResponseNote = new string('a', 1001)
        }).ShouldHaveValidationErrorFor(x => x.OwnerResponseNote);
    }

    // ----------------------------------------------------------------- reviews

    [Fact]
    public void A_complete_review_is_accepted()
    {
        _review.TestValidate(Review()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rule_40_a_rating_outside_one_to_five_is_refused(int rating)
    {
        _review.TestValidate(Review(rating: rating)).ShouldHaveValidationErrorFor(x => x.Rating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Rule_40_both_ends_of_the_scale_are_accepted(int rating)
    {
        _review.TestValidate(Review(rating: rating)).ShouldNotHaveValidationErrorFor(x => x.Rating);
    }

    [Fact]
    public void A_review_without_a_comment_is_refused()
    {
        _review.TestValidate(Review(comment: "   ")).ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Fact]
    public void A_comment_longer_than_a_thousand_characters_is_refused()
    {
        _review.TestValidate(Review(comment: new string('a', 1001)))
            .ShouldHaveValidationErrorFor(x => x.Comment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Lived")]
    [InlineData("7")]
    public void A_stay_status_the_product_does_not_have_is_refused(string stay)
    {
        _review.TestValidate(Review(stay: stay)).ShouldHaveValidationErrorFor(x => x.StayStatus);
    }

    [Theory]
    [InlineData("Visited")]
    [InlineData("CurrentlyStaying")]
    [InlineData("previouslystayed")]
    public void Every_stay_status_is_accepted(string stay)
    {
        _review.TestValidate(Review(stay: stay)).ShouldNotHaveValidationErrorFor(x => x.StayStatus);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("hidden")]
    public void A_review_is_moderated_to_approved_or_hidden(string state)
    {
        _moderate.TestValidate(new ModerateReviewCommand { State = state })
            .ShouldNotHaveValidationErrorFor(x => x.State);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Deleted")]
    [InlineData("")]
    [InlineData("4")]
    public void Rule_96_nothing_else_may_be_set_on_a_review(string state)
    {
        _moderate.TestValidate(new ModerateReviewCommand { State = state })
            .ShouldHaveValidationErrorFor(x => x.State);
    }

    // ----------------------------------------------------------------- reports

    [Fact]
    public void A_complete_report_is_accepted()
    {
        _report.TestValidate(Report()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Rude")]
    [InlineData("9")]
    public void A_reason_the_product_does_not_have_is_refused(string reason)
    {
        _report.TestValidate(Report(reason: reason)).ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData("Inaccurate")]
    [InlineData("Misleading")]
    [InlineData("AlreadyTaken")]
    [InlineData("Scam")]
    [InlineData("Offensive")]
    [InlineData("other")]
    public void Every_reason_is_accepted(string reason)
    {
        _report.TestValidate(Report(reason: reason)).ShouldNotHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void A_report_without_details_is_refused()
    {
        _report.TestValidate(Report(details: " ")).ShouldHaveValidationErrorFor(x => x.Details);
    }

    [Theory]
    [InlineData("UnderReview")]
    [InlineData("Upheld")]
    [InlineData("dismissed")]
    public void A_report_is_moved_to_one_of_three_states(string status)
    {
        _resolve.TestValidate(new ResolveReportCommand { Status = status })
            .ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Closed")]
    [InlineData("")]
    [InlineData("8")]
    public void Rule_97_nothing_else_may_be_set_on_a_report(string status)
    {
        _resolve.TestValidate(new ResolveReportCommand { Status = status })
            .ShouldHaveValidationErrorFor(x => x.Status);
    }

    // ------------------------------------------------------------------- pages

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void Rule_30_a_page_outside_the_limits_is_refused(int page, int pageSize)
    {
        new GetSavedPropertiesQueryValidator()
            .TestValidate(new GetSavedPropertiesQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        new GetMyVisitsQueryValidator()
            .TestValidate(new GetMyVisitsQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        new GetOwnerVisitsQueryValidator()
            .TestValidate(new GetOwnerVisitsQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        new GetPropertyReviewsQueryValidator()
            .TestValidate(new GetPropertyReviewsQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        new GetReviewQueueQueryValidator()
            .TestValidate(new GetReviewQueueQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        new GetReportQueueQueryValidator()
            .TestValidate(new GetReportQueueQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_unknown_queue_filter_is_refused()
    {
        new GetReviewQueueQueryValidator()
            .TestValidate(new GetReviewQueueQuery { State = "Deleted" })
            .ShouldHaveValidationErrorFor(x => x.State);

        new GetReportQueueQueryValidator()
            .TestValidate(new GetReportQueueQuery { Status = "Closed" })
            .ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void An_omitted_queue_filter_means_everything()
    {
        new GetReviewQueueQueryValidator()
            .TestValidate(new GetReviewQueueQuery())
            .ShouldNotHaveAnyValidationErrors();

        new GetReportQueueQueryValidator()
            .TestValidate(new GetReportQueueQuery())
            .ShouldNotHaveAnyValidationErrors();
    }
}
