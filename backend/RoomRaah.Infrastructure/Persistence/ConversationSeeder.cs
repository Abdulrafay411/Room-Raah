using Microsoft.EntityFrameworkCore;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Three conversations for development, to §4.4 of the build plan.
///
/// One of them is left with messages the seeker has not read, because an unread badge
/// that is always zero is a badge nobody can tell is working. Another is on a listing
/// that already carries a visit request, which is the ordinary case in this product:
/// somebody asks a question, then asks to come and see it.
/// </summary>
public static class ConversationSeeder
{
    public static async Task SeedAsync(RoomRaahDbContext context)
    {
        if (await context.Conversations.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var published = await context.Properties
            .IgnoreQueryFilters()
            .Where(p => p.Status == PropertyStatus.Published && p.IsActive)
            .OrderBy(p => p.Id)
            .Take(3)
            .Select(p => new { p.Id, p.OwnerId })
            .ToListAsync();

        var seekers = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Seeker && u.IsEmailVerified)
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync();

        if (published.Count < 3 || seekers.Count < 2)
        {
            return;
        }

        var now = DateTime.UtcNow;

        // The listing the seeded visit request is also on, so the two areas of the
        // product show the same pair of people doing the ordinary thing.
        var first = Thread(published[0].Id, seekers[0], published[0].OwnerId, now.AddDays(-4),
            (seekers[0], "Assalam o alaikum. Is the room still free from the 1st?", now.AddDays(-4), now.AddDays(-4)),
            (published[0].OwnerId, "Walaikum assalam. Yes, one bed is free from the 1st.", now.AddDays(-4).AddMinutes(40), now.AddDays(-3)),
            (seekers[0], "Great. I have sent a visit request for Saturday.", now.AddDays(-3), now.AddDays(-3)));

        // Answered and fully read, so a thread with a zero badge is on the screen too.
        var second = Thread(published[1].Id, seekers[1], published[1].OwnerId, now.AddDays(-2),
            (seekers[1], "Does the rent include electricity in summer?", now.AddDays(-2), now.AddDays(-2)),
            (published[1].OwnerId, "Electricity is billed separately, roughly 2,500 in summer.", now.AddDays(-2).AddHours(3), now.AddDays(-2)));

        // The unread one. Both of the owner's messages are unread by the seeker, so the
        // inbox shows a count of two rather than a badge that only ever says one.
        var third = Thread(published[2].Id, seekers[0], published[2].OwnerId, now.AddHours(-5),
            (seekers[0], "Is there a separate entrance for the upper portion?", now.AddHours(-9), now.AddHours(-8)),
            (published[2].OwnerId, "Yes, the stairs are separate from the main gate.", now.AddHours(-6), null),
            (published[2].OwnerId, "You can come and see it any evening this week.", now.AddHours(-5), null));

        context.Conversations.AddRange(first, second, third);

        await context.SaveChangesAsync();
    }

    private static Conversation Thread(
        int propertyId,
        int seekerId,
        int ownerId,
        DateTime lastMessageAt,
        params (int SenderId, string Body, DateTime SentAt, DateTime? ReadAt)[] messages)
    {
        return new Conversation
        {
            PropertyId = propertyId,
            SeekerId = seekerId,
            OwnerId = ownerId,
            CreatedAt = messages[0].SentAt,
            LastMessageAt = lastMessageAt,
            Messages = messages.Select(m => new Message
            {
                SenderId = m.SenderId,
                Body = m.Body,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt
            }).ToList()
        };
    }
}
