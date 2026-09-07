using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class SeekerProfileConfiguration : IEntityTypeConfiguration<SeekerProfile>
{
    public void Configure(EntityTypeBuilder<SeekerProfile> builder)
    {
        builder.ToTable("SeekerProfiles");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.MinBudget).HasPrecision(18, 2);
        builder.Property(p => p.MaxBudget).HasPrecision(18, 2);

        builder.Property(p => p.GenderPreference)
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.HasIndex(p => p.AppUserId).IsUnique();

        builder.HasOne(p => p.AppUser)
            .WithOne(u => u.SeekerProfile)
            .HasForeignKey<SeekerProfile>(p => p.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Both preferences are stored as foreign keys without navigation properties.
        // Nothing in the product reads the city or landmark row through the profile,
        // and a navigation here would add an Include to queries that only need the id.
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(p => p.PreferredCityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Landmark>()
            .WithMany()
            .HasForeignKey(p => p.PreferredLandmarkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => p.AppUser.IsActive);
    }
}
