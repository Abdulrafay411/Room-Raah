using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class BadgeEventConfiguration : IEntityTypeConfiguration<BadgeEvent>
{
    public void Configure(EntityTypeBuilder<BadgeEvent> builder)
    {
        builder.ToTable("BadgeEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        // Required in the database as well as in the handler. Rule 112 is the whole
        // reason this table exists — a row here with no reason would be a badge change
        // nobody can account for, which is the thing §Business Model calls worse than
        // no badge at all.
        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasIndex(e => new { e.PropertyId, e.ActedAt });

        builder.HasOne(e => e.Property)
            .WithMany(p => p.BadgeEvents)
            .HasForeignKey(e => e.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ActedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.ActedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => e.Property.IsActive);
    }
}
