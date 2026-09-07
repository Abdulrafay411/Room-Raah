using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class PropertyRevisionConfiguration : IEntityTypeConfiguration<PropertyRevision>
{
    public void Configure(EntityTypeBuilder<PropertyRevision> builder)
    {
        builder.ToTable("PropertyRevisions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Kind)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.ReviewStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(r => r.AddressLine)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(r => r.RoomType)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.GenderPolicy)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(r => r.MonthlyRent).HasPrecision(18, 2);
        builder.Property(r => r.SecurityDeposit).HasPrecision(18, 2);
        builder.Property(r => r.UtilitiesCharge).HasPrecision(18, 2);
        builder.Property(r => r.MessCharge).HasPrecision(18, 2);

        builder.Property(r => r.HouseRules).HasMaxLength(2000);

        // The proposed facility list and the proposed gallery are stored as ordered
        // comma-separated ids rather than as join tables. Nothing ever queries a
        // revision by facility or by photo: a revision is written whole, read whole
        // by one admin, and then applied or thrown away. A join table would buy
        // referential integrity on rows that live for a day and cost two more tables
        // and two more query filters. The order matters in PhotoOrder, and a join
        // table has no order of its own.
        builder.Property(r => r.FacilityIds)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(r => r.PhotoOrder).HasMaxLength(400);

        // No foreign key. The photo it names may be deleted before the revision is
        // decided, and a revision is a record of what was asked for rather than a
        // pointer that has to stay valid. Null means the primary is unchanged.

        builder.Property(r => r.AdminNote).HasMaxLength(1000);

        // The two queues read this: pending revisions of one kind, oldest first.
        builder.HasIndex(r => new { r.ReviewStatus, r.Kind, r.SubmittedAt });
        builder.HasIndex(r => new { r.PropertyId, r.ReviewStatus, r.Kind });

        builder.HasOne(r => r.Property)
            .WithMany(p => p.Revisions)
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // The admin who decided is referenced by id alone, with no navigation. A
        // decision belongs to the revision for the rest of the record's life, so the
        // account behind it must never be deletable on that basis.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(r => r.ReviewedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => r.Property.IsActive);
    }
}
