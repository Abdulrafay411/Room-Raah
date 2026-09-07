using FluentValidation;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

/// <summary>
/// Rules 88, 90 and 91 walked over every value of <see cref="VisitStatus"/>.
///
/// The point of the theory is what it catches that a handler test cannot: a handler
/// test proves the transition is refused from the two or three states somebody
/// thought to set up, and says nothing about the rest. A status added later arrives
/// here already covered, and the test fails until somebody decides what it permits.
/// </summary>
public class VisitLifecycleTests
{
    private static readonly VisitStatus[] Answerable = [VisitStatus.Requested];

    private static readonly VisitStatus[] Completable = [VisitStatus.Accepted];

    private static readonly VisitStatus[] Cancellable = [VisitStatus.Requested, VisitStatus.Accepted];

    public static TheoryData<VisitStatus> EveryStatus()
    {
        var data = new TheoryData<VisitStatus>();

        foreach (var status in Enum.GetValues<VisitStatus>())
        {
            data.Add(status);
        }

        return data;
    }

    [Fact]
    public void The_theory_covers_every_status()
    {
        EveryStatus().Count.ShouldBe(Enum.GetValues<VisitStatus>().Length);
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_88_only_a_requested_visit_can_be_answered(VisitStatus status)
    {
        VisitLifecycle.CanAnswer(status).ShouldBe(Answerable.Contains(status));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_90_only_an_accepted_visit_can_be_completed(VisitStatus status)
    {
        VisitLifecycle.CanComplete(status).ShouldBe(Completable.Contains(status));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_91_only_an_open_visit_can_be_cancelled(VisitStatus status)
    {
        VisitLifecycle.CanCancel(status).ShouldBe(Cancellable.Contains(status));
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_88_the_guard_refuses_every_status_the_predicate_refuses(VisitStatus status)
    {
        var visit = TestData.Visit(status: status);

        if (Answerable.Contains(status))
        {
            Should.NotThrow(() => VisitLifecycle.RequireAnswerable(visit));
        }
        else
        {
            Should.Throw<BusinessRuleException>(() => VisitLifecycle.RequireAnswerable(visit));
        }
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_90_the_guard_refuses_every_status_the_predicate_refuses(VisitStatus status)
    {
        var visit = TestData.Visit(status: status, preferredAt: DateTime.UtcNow.AddDays(-1));

        if (Completable.Contains(status))
        {
            Should.NotThrow(() => VisitLifecycle.RequireCompletable(visit, DateTime.UtcNow));
        }
        else
        {
            Should.Throw<BusinessRuleException>(
                () => VisitLifecycle.RequireCompletable(visit, DateTime.UtcNow));
        }
    }

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void Rule_91_the_guard_refuses_every_status_the_predicate_refuses(VisitStatus status)
    {
        var visit = TestData.Visit(status: status);

        if (Cancellable.Contains(status))
        {
            Should.NotThrow(() => VisitLifecycle.RequireCancellable(visit));
        }
        else
        {
            Should.Throw<BusinessRuleException>(() => VisitLifecycle.RequireCancellable(visit));
        }
    }

    /// <summary>
    /// The half of rule 90 that keeps rule 38 honest. Without it, a seeker and an
    /// owner could arrange a visit for next month, complete it this afternoon, and
    /// the review that follows would be of a room nobody had been inside.
    /// </summary>
    [Fact]
    public void Rule_90_a_visit_whose_time_has_not_come_cannot_have_happened()
    {
        var visit = TestData.Visit(
            status: VisitStatus.Accepted, preferredAt: DateTime.UtcNow.AddHours(1));

        Should.Throw<BusinessRuleException>(
            () => VisitLifecycle.RequireCompletable(visit, DateTime.UtcNow));
    }

    [Fact]
    public void Rule_90_a_visit_whose_time_has_passed_can_be_completed()
    {
        var visit = TestData.Visit(
            status: VisitStatus.Accepted, preferredAt: DateTime.UtcNow.AddMinutes(-1));

        Should.NotThrow(() => VisitLifecycle.RequireCompletable(visit, DateTime.UtcNow));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rule_89_a_decline_without_a_reason_is_refused(string? note)
    {
        Should.Throw<ValidationException>(
            () => VisitLifecycle.RequireNoteOnDecline(VisitStatus.Declined, note));
    }

    [Fact]
    public void Rule_89_an_acceptance_needs_no_note()
    {
        VisitLifecycle.RequireNoteOnDecline(VisitStatus.Accepted, null).ShouldBeNull();
    }

    [Fact]
    public void Rule_89_the_note_is_trimmed()
    {
        VisitLifecycle.RequireNoteOnDecline(VisitStatus.Declined, "  both beds are taken  ")
            .ShouldBe("both beds are taken");
    }
}
