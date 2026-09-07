using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class PropertyInspectionConfiguration : IEntityTypeConfiguration<PropertyInspection>
{
    public void Configure(EntityTypeBuilder<PropertyInspection> builder)
    {
        builder.ToTable("PropertyInspections");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Result)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(i => i.Notes).HasMaxLength(2000);

        builder.Property(i => i.FeeAmount).HasColumnType("decimal(18,2)");

        // Rule 110 asks for the latest verdict on a listing, and the admin's view of a
        // listing asks for the trail. Both are this index.
        builder.HasIndex(i => new { i.PropertyId, i.InspectedAt });

        builder.HasOne(i => i.Property)
            .WithMany(p => p.Inspections)
            .HasForeignKey(i => i.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InspectorAdmin)
            .WithMany()
            .HasForeignKey(i => i.InspectorAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => i.Property.IsActive);
    }
}
