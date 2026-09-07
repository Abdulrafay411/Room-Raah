using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class VerificationCheckConfiguration : IEntityTypeConfiguration<VerificationCheck>
{
    public void Configure(EntityTypeBuilder<VerificationCheck> builder)
    {
        builder.ToTable("VerificationChecks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CheckType)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(c => c.Note).HasMaxLength(1000);

        // Rule 64: one row per listing per check type. The uniqueness is in the
        // database as well as in the handler, because rule 23 asks a single question
        // of each check and a second row would make that question ambiguous.
        builder.HasIndex(c => new { c.PropertyId, c.CheckType }).IsUnique();

        builder.HasOne(c => c.Property)
            .WithMany(p => p.VerificationChecks)
            .HasForeignKey(c => c.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(c => c.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => c.Property.IsActive);
    }
}
