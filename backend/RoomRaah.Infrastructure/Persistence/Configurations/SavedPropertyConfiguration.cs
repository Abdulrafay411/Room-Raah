using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class SavedPropertyConfiguration : IEntityTypeConfiguration<SavedProperty>
{
    public void Configure(EntityTypeBuilder<SavedProperty> builder)
    {
        builder.ToTable("SavedProperties");

        builder.HasKey(s => s.Id);

        // Rule 34, in the database as well as in the handler. Two clicks on a save
        // button race each other, and the handler's check cannot see the other one.
        builder.HasIndex(s => new { s.UserId, s.PropertyId }).IsUnique();

        // The shortlist page reads this order.
        builder.HasIndex(s => new { s.UserId, s.SavedAt });

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Property)
            .WithMany()
            .HasForeignKey(s => s.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => s.User.IsActive && s.Property.IsActive);
    }
}
