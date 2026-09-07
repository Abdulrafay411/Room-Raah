using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(p => p.AddressLine)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.HouseRules)
            .HasMaxLength(2000);

        builder.Property(p => p.RoomType)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.GenderPolicy)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.MonthlyRent).HasPrecision(18, 2);
        builder.Property(p => p.SecurityDeposit).HasPrecision(18, 2);
        builder.Property(p => p.UtilitiesCharge).HasPrecision(18, 2);
        builder.Property(p => p.MessCharge).HasPrecision(18, 2);

        builder.HasIndex(p => new { p.Status, p.AreaId });
        builder.HasIndex(p => new { p.Status, p.MonthlyRent });
        builder.HasIndex(p => new { p.Latitude, p.Longitude });
        builder.HasIndex(p => p.AvailabilityConfirmedAt);
        builder.HasIndex(p => p.OwnerId);

        builder.HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Area)
            .WithMany()
            .HasForeignKey(p => p.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mirrors the filters of both required principals as well as its own. A
        // required navigation must never be left pointing at a row the filter has
        // removed, and a deactivated owner or a deactivated area should take the
        // listings that depend on it out of the lists too. The admin panel reads
        // these rows with IgnoreQueryFilters when it needs to see them anyway.
        builder.HasQueryFilter(p =>
            p.IsActive && p.Owner.IsActive && p.Area.IsActive && p.Area.City.IsActive);
    }
}
