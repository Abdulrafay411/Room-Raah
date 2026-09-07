using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Body)
            .IsRequired()
            .HasMaxLength(2000);

        // Thread paging.
        builder.HasIndex(m => new { m.ConversationId, m.SentAt });

        // Rule 104's update and the unread count for an inbox both ask the same
        // question: in this thread, what did somebody else send that is still unread.
        builder.HasIndex(m => new { m.ConversationId, m.SenderId, m.ReadAt });

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mirrors the conversation's filter and deliberately does not add the sender's.
        // A sender is always one of the two participants the conversation filter already
        // covers, and filtering on them here would empty half a thread rather than
        // remove it — a conversation with one side's messages missing is worse to read
        // than one that is gone.
        builder.HasQueryFilter(m =>
            m.Conversation.Seeker.IsActive &&
            m.Conversation.Owner.IsActive &&
            m.Conversation.Property.IsActive);
    }
}
