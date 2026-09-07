using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class OwnerProfileConfiguration : IEntityTypeConfiguration<OwnerProfile>
{
    public void Configure(EntityTypeBuilder<OwnerProfile> builder)
    {
        builder.ToTable("OwnerProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CnicNumber)
            .IsRequired()
            .HasMaxLength(13);

        builder.Property(p => p.IdentityStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.PayoutContact)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(p => p.AppUserId).IsUnique();

        builder.HasOne(p => p.AppUser)
            .WithOne(u => u.OwnerProfile)
            .HasForeignKey<OwnerProfile>(p => p.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.AppUser.IsActive);
    }
}
