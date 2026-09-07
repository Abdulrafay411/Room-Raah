using Microsoft.EntityFrameworkCore;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Shortlists, visit requests, reviews and reports for development.
///
/// One row here is not decoration. Rule 90 says a visit reaches <c>Completed</c>
/// only once its preferred time has passed, and the validator refuses a preferred
/// time that is not in the future — so a run of the product that starts from an
/// empty database can never, by itself, produce a visit that is old enough to
/// complete, and therefore can never produce a review either. The accepted request
/// dated two days ago is what makes the whole path walkable from a clean checkout
/// without waiting or moving the clock.
/// </summary>
public static class SeekerActivitySeeder
{
    public static async Task SeedAsync(RoomRaahDbContext context)
    {
        if (await context.VisitRequests.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var published = await context.Properties
            .IgnoreQueryFilters()
            .Where(p => p.Status == PropertyStatus.Published && p.IsActive)
            .OrderBy(p => p.Id)
            .Take(4)
            .Select(p => p.Id)
            .ToListAsync();

        var seekers = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Seeker && u.IsEmailVerified)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync();

        if (published.Count < 4 || seekers.Count < 3)
        {
            return;
        }

        var admin = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var (first, second, third) = (seekers[0], seekers[1], seekers[2]);

        context.VisitRequests.AddRange(
            // The one the whole review path hangs off: accepted, and its time has
            // already passed, so either party can complete it today.
            Visit(published[0], first, VisitType.Physical, now.AddDays(-2), VisitStatus.Accepted,
                "Come any time after 4pm, ask for the ground floor portion.", now.AddDays(-4)),

            // Waiting for the owner, so the respond endpoint has something real to answer.
            Visit(published[1], first, VisitType.Video, now.AddDays(3), VisitStatus.Requested,
                null, now.AddHours(-6)),

            // The two completed visits behind the two seeded reviews.
            Visit(published[0], second, VisitType.Physical, now.AddDays(-10), VisitStatus.Completed,
                "Accepted, see you Saturday.", now.AddDays(-13)),

            Visit(published[0], third, VisitType.Video, now.AddDays(-8), VisitStatus.Completed,
                null, now.AddDays(-11)),

            // Rule 89: a decline carries its reason.
            Visit(published[2], second, VisitType.Physical, now.AddDays(-5), VisitStatus.Declined,
                "Both beds were taken last week. I will list again in March.", now.AddDays(-7)),

            // The fifth status, so every value of VisitStatus is on the screen somewhere.
            Visit(published[2], third, VisitType.Video, now.AddDays(-3), VisitStatus.Cancelled,
                null, now.AddDays(-6)),

            // Three more completed visits, one behind each of the three reviews below.
            // Rule 38 is why they exist: a review with no completed visit is seed data
            // that breaks a rule the product enforces, which looks right in the database
            // and makes the first screen a lie.
            Visit(published[1], second, VisitType.Physical, now.AddDays(-9), VisitStatus.Completed,
                "Accepted, come after Asr.", now.AddDays(-12)),

            Visit(published[1], third, VisitType.Physical, now.AddDays(-7), VisitStatus.Completed,
                null, now.AddDays(-10)),

            Visit(published[0], first, VisitType.Physical, now.AddDays(-14), VisitStatus.Completed,
                null, now.AddDays(-16)));

        // One approved review so the public list has something in it, and one still
        // pending so the moderation queue does too.
        context.Reviews.AddRange(
            new Review
            {
                PropertyId = published[0],
                AuthorId = second,
                Rating = 4,
                Comment = "Clean room and the mess food is better than I expected. " +
                          "The street gets noisy in the evening, but the room itself is quiet.",
                StayStatus = StayStatus.CurrentlyStaying,
                ModerationState = ModerationState.Approved,
                ModeratedByAdminId = admin,
                ModeratedAt = now.AddDays(-6),
                CreatedAt = now.AddDays(-7)
            },
            new Review
            {
                PropertyId = published[0],
                AuthorId = third,
                Rating = 3,
                Comment = "The room matches the photographs. Water pressure on the top floor is weak in the mornings.",
                StayStatus = StayStatus.Visited,
                ModerationState = ModerationState.Pending,
                CreatedAt = now.AddDays(-1)
            },

            // Two more approved, on a second listing, so the public review list is not
            // one listing's private feature and the average is computed from more than
            // a single number.
            new Review
            {
                PropertyId = published[1],
                AuthorId = second,
                Rating = 5,
                Comment = "The owner was straight about the charges and the room was exactly as listed. " +
                          "Mess timings are fixed, which suits me.",
                StayStatus = StayStatus.PreviouslyStayed,
                ModerationState = ModerationState.Approved,
                ModeratedByAdminId = admin,
                ModeratedAt = now.AddDays(-8),
                CreatedAt = now.AddDays(-9)
            },
            new Review
            {
                PropertyId = published[1],
                AuthorId = third,
                Rating = 4,
                Comment = "Good value for the area. The Wi-Fi drops in the evening when everyone is back.",
                StayStatus = StayStatus.CurrentlyStaying,
                ModerationState = ModerationState.Approved,
                ModeratedByAdminId = admin,
                ModeratedAt = now.AddDays(-5),
                CreatedAt = now.AddDays(-6)
            },

            // And one an admin took down, so the moderation screen shows all three
            // states and the public list can be checked for not carrying this one.
            new Review
            {
                PropertyId = published[0],
                AuthorId = first,
                Rating = 1,
                Comment = "This review was hidden by an administrator.",
                StayStatus = StayStatus.Visited,
                ModerationState = ModerationState.Hidden,
                ModeratedByAdminId = admin,
                ModeratedAt = now.AddDays(-2),
                CreatedAt = now.AddDays(-4)
            });

        context.SavedProperties.AddRange(
            new SavedProperty { UserId = first, PropertyId = published[0], SavedAt = now.AddDays(-5) },
            new SavedProperty { UserId = first, PropertyId = published[1], SavedAt = now.AddDays(-3) },
            new SavedProperty { UserId = first, PropertyId = published[2], SavedAt = now.AddHours(-2) },
            new SavedProperty { UserId = second, PropertyId = published[0], SavedAt = now.AddDays(-12) });

        context.Reports.AddRange(
            new Report
            {
                PropertyId = published[3],
                ReportedByUserId = second,
                Reason = ReportReason.AlreadyTaken,
                Details = "I visited on Tuesday and the owner said the room had gone a month ago. " +
                          "The listing still says two beds are free.",
                Status = ReportStatus.Open,
                CreatedAt = now.AddDays(-2)
            },

            // One already decided, so the admin queue shows both ends of rule 97 — and
            // note that the listing it names is still published, which is rule 98.
            new Report
            {
                PropertyId = published[1],
                ReportedByUserId = third,
                Reason = ReportReason.Misleading,
                Details = "The photographs show a room with a window. The room I saw has no window.",
                Status = ReportStatus.Upheld,
                ResolvedByAdminId = admin,
                ResolvedAt = now.AddDays(-3),
                AdminNote = "Photographs did not match the room. The owner has been asked to replace them.",
                CreatedAt = now.AddDays(-6)
            });

        await context.SaveChangesAsync();
    }

    private static VisitRequest Visit(
        int propertyId,
        int seekerId,
        VisitType type,
        DateTime preferredAt,
        VisitStatus status,
        string? note,
        DateTime createdAt) =>
        new()
        {
            PropertyId = propertyId,
            SeekerId = seekerId,
            VisitType = type,
            PreferredAt = preferredAt,
            Status = status,
            OwnerResponseNote = note,
            RespondedAt = status == VisitStatus.Requested ? null : createdAt.AddHours(5),
            CreatedAt = createdAt
        };
}
