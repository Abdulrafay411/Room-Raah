using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly RoomRaahDbContext _context;

    public ConversationRepository(RoomRaahDbContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Conversation conversation)
    {
        _context.Conversations.Add(conversation);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tracked, because sending a message writes <c>LastMessageAt</c> onto it. Both
    /// participants come with it: rule 102 needs neither of them loaded, but naming the
    /// other one and emailing them both do.
    /// </summary>
    public Task<Conversation?> GetByIdAsync(int id)
    {
        return WithContext().FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Rule 43.</summary>
    public Task<Conversation?> GetForSeekerAndPropertyAsync(int seekerId, int propertyId)
    {
        return WithContext()
            .FirstOrDefaultAsync(c => c.SeekerId == seekerId && c.PropertyId == propertyId);
    }

    /// <summary>
    /// Rule 101. One method for both inboxes: which side of the conversation the caller
    /// is on decides nothing about how the list is built, and writing it twice would be
    /// two places for the ordering to drift.
    /// </summary>
    public Task<List<Conversation>> GetForUserAsync(int userId, int page, int pageSize)
    {
        return Visible(userId)
            .OrderByDescending(c => c.LastMessageAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public Task<int> CountForUserAsync(int userId)
    {
        return Visible(userId).CountAsync();
    }

    /// <summary>
    /// The caller's own threads and nobody else's, less any the caller has deleted and
    /// nobody has written in since — rules 129 and 131, decided by the caller's own side
    /// of each thread. Held in one place so the page and the count can never answer
    /// different questions.
    /// </summary>
    private IQueryable<Conversation> Visible(int userId)
    {
        return WithContext().Where(c =>
            (c.SeekerId == userId && !c.HiddenFromSeeker) ||
            (c.OwnerId == userId && !c.HiddenFromOwner));
    }

    private IQueryable<Conversation> WithContext()
    {
        return _context.Conversations
            .Include(c => c.Property)
            .Include(c => c.Seeker)
            .Include(c => c.Owner);
    }
}
