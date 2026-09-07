using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.Details)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.AdminNote).HasMaxLength(1000);

        // Rule 42 as amended: one unresolved report per person per listing.
        builder.HasIndex(r => new { r.ReportedByUserId, r.PropertyId, r.Status });

        // The admin queue, oldest first.
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.HasOne(r => r.Property)
            .WithMany()
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ReportedByUser)
            .WithMany()
            .HasForeignKey(r => r.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.ResolvedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => r.ReportedByUser.IsActive && r.Property.IsActive);
    }
}
