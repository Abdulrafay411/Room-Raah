using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly RoomRaahDbContext _context;

    public MessageRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Message message)
    {
        _context.Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<List<Message>> GetForConversationAsync(
        int conversationId, int page, int pageSize, DateTime? sentAfter = null)
    {
        // Oldest first, which is the order a conversation is read in — and broken by
        // the id for the reason rule 77 gives about search: two messages sent in the
        // same millisecond are otherwise free to swap places between pages.
        return InThread(conversationId, sentAfter)
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountForConversationAsync(int conversationId, DateTime? sentAfter = null)
    {
        return InThread(conversationId, sentAfter).CountAsync();
    }

    /// <summary>
    /// One thread, cut at rule 130's line when there is one. The page and its count go
    /// through the same filter, so a deleted history can never show up as a page that
    /// claims more messages than it holds.
    /// </summary>
    private IQueryable<Message> InThread(int conversationId, DateTime? sentAfter)
    {
        var messages = _context.Messages.Where(m => m.ConversationId == conversationId);

        return sentAfter is null ? messages : messages.Where(m => m.SentAt > sentAfter);
    }

    /// <summary>
    /// Rule 104, as one statement.
    ///
    /// <c>ExecuteUpdateAsync</c> rather than loading the unread messages and setting a
    /// property on each: the answer is a count, so there is nothing to bring back, and
    /// a thread with two hundred unread messages costs the same as one with two.
    /// </summary>
    public Task<int> MarkReadAsync(int conversationId, int readerId, DateTime readAt)
    {
        return _context.Messages
            .Where(m =>
                m.ConversationId == conversationId &&
                m.SenderId != readerId &&
                m.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAt, readAt));
    }

    /// <summary>
    /// One query for a whole page of threads. Grouped in the database rather than
    /// counted per conversation, because an inbox of twenty threads would otherwise be
    /// twenty round trips — the shape that only ever hurts once there is real data.
    /// </summary>
    public async Task<Dictionary<int, int>> GetUnreadCountsAsync(
        int readerId, IReadOnlyCollection<int> conversationIds)
    {
        if (conversationIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var counts = await _context.Messages
            .Where(m =>
                conversationIds.Contains(m.ConversationId) &&
                m.SenderId != readerId &&
                m.ReadAt == null)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(c => c.ConversationId, c => c.Count);
    }
}
