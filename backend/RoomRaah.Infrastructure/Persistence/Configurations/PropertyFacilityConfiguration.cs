using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class PropertyFacilityConfiguration : IEntityTypeConfiguration<PropertyFacility>
{
    public void Configure(EntityTypeBuilder<PropertyFacility> builder)
    {
        builder.ToTable("PropertyFacilities");

        builder.HasKey(pf => new { pf.PropertyId, pf.FacilityId });

        builder.HasOne(pf => pf.Property)
            .WithMany(p => p.Facilities)
            .HasForeignKey(pf => pf.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pf => pf.Facility)
            .WithMany()
            .HasForeignKey(pf => pf.FacilityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Same reasoning as PropertyPhoto: a join row is reached through its
        // property, and it mirrors the facility's own filter so a deactivated
        // facility stops appearing on the listings that reference it.
        builder.HasQueryFilter(pf => pf.Property.IsActive && pf.Facility.IsActive);
    }
}
