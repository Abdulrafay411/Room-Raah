using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class LandmarkConfiguration : IEntityTypeConfiguration<Landmark>
{
    public void Configure(EntityTypeBuilder<Landmark> builder)
    {
        builder.ToTable("Landmarks");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Kind)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.HasIndex(l => new { l.CityId, l.Name }).IsUnique();

        builder.HasOne(l => l.City)
            .WithMany(c => c.Landmarks)
            .HasForeignKey(l => l.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(l => l.IsActive && l.City.IsActive);
    }
}
