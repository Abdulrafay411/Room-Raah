using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(a => new { a.CityId, a.Name }).IsUnique();

        builder.HasOne(a => a.City)
            .WithMany(c => c.Areas)
            .HasForeignKey(a => a.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mirrors the city's own filter as well as its own: a required navigation
        // must never be left pointing at a principal the filter has removed, and
        // deactivating a city should take its areas out of the lists with it.
        builder.HasQueryFilter(a => a.IsActive && a.City.IsActive);
    }
}
