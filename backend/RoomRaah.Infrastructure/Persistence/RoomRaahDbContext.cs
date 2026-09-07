using Microsoft.EntityFrameworkCore;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Infrastructure.Persistence;

public class RoomRaahDbContext : DbContext
{
    public RoomRaahDbContext(DbContextOptions<RoomRaahDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<SeekerProfile> SeekerProfiles => Set<SeekerProfile>();
    public DbSet<OwnerProfile> OwnerProfiles => Set<OwnerProfile>();
    public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Landmark> Landmarks => Set<Landmark>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyPhoto> PropertyPhotos => Set<PropertyPhoto>();
    public DbSet<PropertyFacility> PropertyFacilities => Set<PropertyFacility>();
    public DbSet<PropertyRevision> PropertyRevisions => Set<PropertyRevision>();
    public DbSet<VerificationCheck> VerificationChecks => Set<VerificationCheck>();
    public DbSet<SavedProperty> SavedProperties => Set<SavedProperty>();
    public DbSet<VisitRequest> VisitRequests => Set<VisitRequest>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<PropertyInspection> PropertyInspections => Set<PropertyInspection>();
    public DbSet<BadgeEvent> BadgeEvents => Set<BadgeEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoomRaahDbContext).Assembly);
    }
}
