using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Comment)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.StayStatus)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(r => r.ModerationState)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        // Rule 39, in the database as well as in the handler.
        builder.HasIndex(r => new { r.PropertyId, r.AuthorId }).IsUnique();

        // The public read: rule 41's approved reviews for one listing, newest first.
        builder.HasIndex(r => new { r.PropertyId, r.ModerationState, r.CreatedAt });

        builder.HasOne(r => r.Property)
            .WithMany()
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.ModeratedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => r.Author.IsActive && r.Property.IsActive);
    }
}
