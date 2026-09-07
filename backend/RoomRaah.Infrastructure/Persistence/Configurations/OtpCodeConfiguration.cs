using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCodes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Purpose)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(c => c.CodeHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.AttemptCount).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => new { c.AppUserId, c.Purpose });
        builder.HasIndex(c => c.ExpiresAt);

        builder.HasOne(c => c.AppUser)
            .WithMany(u => u.OtpCodes)
            .HasForeignKey(c => c.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => c.AppUser.IsActive);
    }
}
