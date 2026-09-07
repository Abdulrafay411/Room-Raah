using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

/// <summary>
/// Rule 115, asserted as a property of the data rather than of a filter.
///
/// The rule says no owner-facing response carries a scheduled inspection date. A test
/// that checked one endpoint would prove one endpoint; this checks that **no such date
/// exists anywhere to leak** — not on the entity, not on the owner's shape. That is the
/// only version of the rule that stays true when somebody adds an endpoint next year.
/// </summary>
public class InspectionShapeTests
{
    private static readonly string[] SchedulingWords =
    [
        "scheduled", "nextinspection", "dueat", "plannedat", "upcoming", "nextvisit"
    ];

    [Fact]
    public void No_inspection_date_in_the_future_is_stored_anywhere()
    {
        var names = typeof(PropertyInspection).GetProperties().Select(p => p.Name).ToList();

        foreach (var word in SchedulingWords)
        {
            names.ShouldNotContain(
                n => n.Contains(word, StringComparison.OrdinalIgnoreCase),
                $"PropertyInspection has a field that reads like a scheduled visit. "
                + "Rule 115 holds because no such date exists in the product at all.");
        }
    }

    [Fact]
    public void The_listing_itself_holds_no_scheduled_visit_either()
    {
        var names = typeof(Property).GetProperties().Select(p => p.Name).ToList();

        foreach (var word in SchedulingWords)
        {
            names.ShouldNotContain(n => n.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// What the owner is told: whether their one request has been made, and what has
    /// already happened. Nothing about what is coming.
    /// </summary>
    [Fact]
    public void The_owner_sees_the_past_and_their_own_request()
    {
        var names = typeof(OwnerInspectionStateDto).GetProperties().Select(p => p.Name).ToList();

        names.ShouldContain(nameof(OwnerInspectionStateDto.InspectionRequested));
        names.ShouldContain(nameof(OwnerInspectionStateDto.LastInspectionAt));
        names.ShouldContain(nameof(OwnerInspectionStateDto.HasInspectionBadge));

        foreach (var word in SchedulingWords)
        {
            names.ShouldNotContain(n => n.Contains(word, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// The owner's shape carries nothing about the inspector or the fee either. Neither
    /// is forbidden by a rule; both are the admin's side of a paid arrangement, and a
    /// shape that carries what it does not need is the shape that leaks later.
    /// </summary>
    [Fact]
    public void The_owner_shape_names_no_inspector_and_no_note()
    {
        var names = typeof(OwnerInspectionStateDto).GetProperties().Select(p => p.Name).ToList();

        names.ShouldNotContain(n => n.Contains("Inspector", StringComparison.OrdinalIgnoreCase));
        names.ShouldNotContain(n => n.Contains("Notes", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Rule 33: the worklist names an owner and gives no way to reach them.</summary>
    [Fact]
    public void The_worklist_names_the_owner_and_nothing_else_about_them()
    {
        var names = typeof(DueForInspectionDto).GetProperties().Select(p => p.Name).ToList();

        names.ShouldContain(nameof(DueForInspectionDto.OwnerDisplayName));
        names.ShouldNotContain("OwnerEmail");
        names.ShouldNotContain("PhoneNumber");
        names.ShouldNotContain("OwnerId");
    }
}
