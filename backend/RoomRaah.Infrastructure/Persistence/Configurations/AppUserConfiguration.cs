using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(u => u.IsEmailVerified).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.DeactivationReason).HasMaxLength(1000);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        // Rule 117's list, which filters on both of these and shows deactivated
        // accounts as a matter of course.
        builder.HasIndex(u => new { u.Role, u.IsActive });

        builder.HasQueryFilter(u => u.IsActive);
    }
}
