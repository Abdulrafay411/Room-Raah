using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class PropertyPhotoConfiguration : IEntityTypeConfiguration<PropertyPhoto>
{
    public void Configure(EntityTypeBuilder<PropertyPhoto> builder)
    {
        builder.ToTable("PropertyPhotos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.BlobUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.ThumbnailUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.HasIndex(p => new { p.PropertyId, p.SortOrder });

        builder.HasOne(p => p.Property)
            .WithMany(p => p.Photos)
            .HasForeignKey(p => p.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // No query filter of its own. A photo is part of the property aggregate and
        // is only ever reached through it, so the property's filter is the one that
        // decides whether a photo is visible. Repeating the chain here would add a
        // four-table join to a query that never runs on its own.
        builder.HasQueryFilter(p => p.Property.IsActive);
    }
}
