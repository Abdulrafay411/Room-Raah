using FluentValidation;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

/// <summary>
/// Rules 96 and 97 walked over every value of both enumerations, for the reason the
/// visit lifecycle theory gives.
/// </summary>
public class ModerationRulesTests
{
    private static readonly ModerationState[] ModerationTargets =
        [ModerationState.Approved, ModerationState.Hidden];

    private static readonly ReportStatus[] ResolutionTargets =
        [ReportStatus.UnderReview, ReportStatus.Upheld, ReportStatus.Dismissed];

    private static readonly ReportStatus[] Verdicts = [ReportStatus.Upheld, ReportStatus.Dismissed];

    public static TheoryData<ModerationState> EveryState()
    {
        var data = new TheoryData<ModerationState>();

        foreach (var state in Enum.GetValues<ModerationState>())
        {
            data.Add(state);
        }

        return data;
    }

    public static TheoryData<ReportStatus> EveryStatus()
    {
        var data = new TheoryData<ReportStatus>();

        foreach (var status in Enum.GetValues<ReportStatus>())
        {
            data.Add(status);
        }

        return data;
    }

    [Fact]
    public void The_theories_cover_every_value()
    {
        EveryState().Count.ShouldBe(Enum.GetValues<ModerationState>().Length);
        EveryStatus().Count.ShouldBe(Enum.GetValues<ReportStatus>().Length);
    }

    [Theory]
    [MemberData(nameof(EveryState))]
    public void Rule_96_a_review_is_moderated_to_approved_or_hidden(ModerationState state)
    {
        ModerationRules.IsModerationTarget(state).ShouldBe(ModerationTargets.Contains(state));
    }

    /// <summary>
    /// Pending is where a review starts and not somewhere an admin puts one back.
    /// Undoing an approval is hiding the review, which leaves a record of what
    /// happened; moving it back to Pending would make it look unread.
    /// </summary>
    [Fact]
    public void Rule_96_a_review_cannot_be_moved_back_to_pending()
    {
        Should.Throw<BusinessRuleException>(
            () => ModerationRules.RequireModerationTarget(ModerationState.Pending));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_97_a_report_is_moved_to_one_of_three_states(ReportStatus status)
    {
        ModerationRules.IsResolutionTarget(status).ShouldBe(ResolutionTargets.Contains(status));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_97_upholding_and_dismissing_are_the_verdicts(ReportStatus status)
    {
        ModerationRules.IsVerdict(status).ShouldBe(Verdicts.Contains(status));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_97_a_report_carrying_a_verdict_is_not_decided_again(ReportStatus current)
    {
        var report = TestData.Report(status: current);

        if (Verdicts.Contains(current))
        {
            Should.Throw<BusinessRuleException>(
                () => ModerationRules.RequireResolvable(report, ReportStatus.Dismissed));
        }
        else
        {
            Should.NotThrow(() => ModerationRules.RequireResolvable(report, ReportStatus.Dismissed));
        }
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_97_only_the_three_targets_are_accepted(ReportStatus target)
    {
        var report = TestData.Report(status: ReportStatus.Open);

        if (ResolutionTargets.Contains(target))
        {
            Should.NotThrow(() => ModerationRules.RequireResolvable(report, target));
        }
        else
        {
            Should.Throw<BusinessRuleException>(
                () => ModerationRules.RequireResolvable(report, target));
        }
    }

    [Theory]
    [InlineData(ReportStatus.Upheld)]
    [InlineData(ReportStatus.Dismissed)]
    public void Rule_97_a_verdict_without_a_note_is_refused(ReportStatus verdict)
    {
        Should.Throw<ValidationException>(
            () => ModerationRules.RequireNoteOnVerdict(verdict, "   "));
    }

    [Fact]
    public void Rule_97_moving_a_report_under_review_needs_no_note()
    {
        ModerationRules.RequireNoteOnVerdict(ReportStatus.UnderReview, null).ShouldBeNull();
    }

    [Fact]
    public void Rule_97_the_note_is_trimmed()
    {
        ModerationRules.RequireNoteOnVerdict(ReportStatus.Upheld, "  the room is gone  ")
            .ShouldBe("the room is gone");
    }
}
