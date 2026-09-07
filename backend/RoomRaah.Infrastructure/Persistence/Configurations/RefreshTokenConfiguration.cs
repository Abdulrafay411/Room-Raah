using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasIndex(t => t.Token).IsUnique();

        builder.HasOne(t => t.AppUser)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.AppUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(t => t.AppUser.IsActive);
    }
}
