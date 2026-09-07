using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class AdminProfileConfiguration : IEntityTypeConfiguration<AdminProfile>
{
    public void Configure(EntityTypeBuilder<AdminProfile> builder)
    {
        builder.ToTable("AdminProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CanInspect).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => p.AppUserId).IsUnique();

        builder.HasOne(p => p.AppUser)
            .WithOne(u => u.AdminProfile)
            .HasForeignKey<AdminProfile>(p => p.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.AppUser.IsActive);
    }
}
