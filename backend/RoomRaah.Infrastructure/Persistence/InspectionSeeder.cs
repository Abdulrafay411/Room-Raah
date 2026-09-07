using Microsoft.EntityFrameworkCore;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Inspections and badge events for development, to §4.4 of the build plan: four
/// visits — three passed, one failed — and five badge events.
///
/// The failed one is not decoration either. It sits on a listing that carries no
/// badge, so the two states an admin has to be able to tell apart are both on the
/// screen: a listing that was visited and passed, and a listing that was visited and
/// did not. A seeder where every visit passed would make the second look like a
/// listing nobody has been to.
/// </summary>
public static class InspectionSeeder
{
    public static async Task SeedAsync(RoomRaahDbContext context)
    {
        if (await context.PropertyInspections.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var inspector = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Admin && u.AdminProfile!.CanInspect)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (inspector is null)
        {
            return;
        }

        var badged = await context.Properties
            .IgnoreQueryFilters()
            .Where(p => p.Status == PropertyStatus.Published && p.IsActive && p.HasInspectionBadge)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToListAsync();

        var unbadged = await context.Properties
            .IgnoreQueryFilters()
            .Where(p => p.Status == PropertyStatus.Published && p.IsActive && !p.HasInspectionBadge)
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (badged.Count < 3 || unbadged == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // The three badged listings were visited ten days ago, which is what the
        // property seeder already stamped on them — so LastInspectionAt and the
        // inspection trail agree, and rule 114's worklist is empty from a clean start
        // rather than immediately full of listings nobody has actually neglected.
        context.PropertyInspections.AddRange(
            badged.Select(id => Inspection(id, inspector.Value, now.AddDays(-10), InspectionResult.Passed,
                "Rooms, facilities and the address all matched the listing.")));

        // The failed one, on a listing with no badge.
        context.PropertyInspections.Add(Inspection(
            unbadged, inspector.Value, now.AddDays(-6), InspectionResult.Failed,
            "Two of the advertised beds were already occupied and the mess was not running."));

        context.BadgeEvents.AddRange(
            badged.Select(id => new BadgeEvent
            {
                PropertyId = id,
                Action = BadgeAction.Granted,
                Reason = "Passed the physical inspection.",
                ActedByAdminId = inspector.Value,
                ActedAt = now.AddDays(-10)
            }));

        // A pair on the listing that failed, so the trail shows both directions and
        // shows them honestly. A removal with no grant in front of it would describe a
        // badge that was taken off a listing that never had one — which is a history
        // that contradicts itself, and exactly the thing an audit trail is for.
        context.BadgeEvents.AddRange(
            new BadgeEvent
            {
                PropertyId = unbadged,
                Action = BadgeAction.Granted,
                Reason = "Passed the first physical inspection.",
                ActedByAdminId = inspector.Value,
                ActedAt = now.AddDays(-70)
            },
            new BadgeEvent
            {
                PropertyId = unbadged,
                Action = BadgeAction.Removed,
                Reason = "Re-inspection failed: the listing no longer matches the property.",
                ActedByAdminId = inspector.Value,
                ActedAt = now.AddDays(-6)
            });

        await context.SaveChangesAsync();
    }

    private static PropertyInspection Inspection(
        int propertyId, int inspectorId, DateTime at, InspectionResult result, string notes)
    {
        return new PropertyInspection
        {
            PropertyId = propertyId,
            InspectedAt = at,
            InspectorAdminId = inspectorId,
            Result = result,
            Notes = notes,
            FeeAmount = 1500m,
            FeeCollectedAt = at.AddDays(-1),
            CreatedAt = at
        };
    }
}
