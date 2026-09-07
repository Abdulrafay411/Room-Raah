using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(c => c.Id);

        // Rule 43, in the database rather than only in the handler. The handler's check
        // is what makes the endpoint return the existing thread instead of an error;
        // this is what makes two requests arriving in the same instant unable to
        // produce two threads between the same two people about the same room.
        builder.HasIndex(c => new { c.SeekerId, c.PropertyId }).IsUnique();

        // Both inboxes, in the order rule 101 asks for.
        builder.HasIndex(c => new { c.SeekerId, c.LastMessageAt });
        builder.HasIndex(c => new { c.OwnerId, c.LastMessageAt });

        builder.HasOne(c => c.Property)
            .WithMany()
            .HasForeignKey(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on both people, for the reason every AppUser foreign key in this
        // product is restricted: a conversation has two of them, and cascading either
        // would give SQL Server two paths to the same row.
        builder.HasOne(c => c.Seeker)
            .WithMany()
            .HasForeignKey(c => c.SeekerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c =>
            c.Seeker.IsActive && c.Owner.IsActive && c.Property.IsActive);
    }
}
