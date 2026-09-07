using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class VisitRequestConfiguration : IEntityTypeConfiguration<VisitRequest>
{
    public void Configure(EntityTypeBuilder<VisitRequest> builder)
    {
        builder.ToTable("VisitRequests");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VisitType)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(v => v.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(v => v.OwnerResponseNote).HasMaxLength(1000);

        // Rules 36 and 38 both ask "does this seeker have a request on this listing in
        // some state", which is this index. Not unique: a declined or cancelled request
        // is finished business and the seeker may ask again.
        builder.HasIndex(v => new { v.SeekerId, v.PropertyId, v.Status });

        // The owner's list.
        builder.HasIndex(v => new { v.PropertyId, v.CreatedAt });

        builder.HasOne(v => v.Property)
            .WithMany()
            .HasForeignKey(v => v.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Seeker)
            .WithMany()
            .HasForeignKey(v => v.SeekerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(v => v.Seeker.IsActive && v.Property.IsActive);
    }
}
