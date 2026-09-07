using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Development seed data. Runs only when the account table is empty, and only
/// outside production. Every seeded account shares one known password.
/// </summary>
public static class DataSeeder
{
    public const string SeedPassword = "Password1";

    public static async Task SeedAsync(RoomRaahDbContext context, IPasswordHasher passwordHasher)
    {
        if (await context.AppUsers.IgnoreQueryFilters().AnyAsync())
        {
            return;
        }

        var hash = passwordHasher.Hash(SeedPassword);
        var now = DateTime.UtcNow;

        // Reference data is seeded first, so two seekers can carry a real preferred
        // city and landmark rather than leaving those foreign keys untouched.
        var lahoreId = await CityIdAsync(context, "Lahore");
        var islamabadId = await CityIdAsync(context, "Islamabad");
        var nustId = await LandmarkIdAsync(context, "NUST");
        var punjabUniversityId = await LandmarkIdAsync(context, "University of the Punjab");

        var users = new List<AppUser>
        {
            NewUser("Ayesha Siddiqui", "admin@roomraah.local", "+923001110001", hash, UserRole.Admin, now,
                admin: new AdminProfile { CanInspect = true }),

            NewUser("Bilal Anwar", "admin2@roomraah.local", "+923001110002", hash, UserRole.Admin, now,
                admin: new AdminProfile { CanInspect = false }),

            NewUser("Kashif Mehmood", "owner.checked@roomraah.local", "+923001110003", hash, UserRole.Owner, now,
                owner: new OwnerProfile
                {
                    CnicNumber = "3520112345671",
                    IdentityStatus = VerificationStatus.Checked,
                    IdentityCheckedAt = now.AddDays(-20),
                    PayoutContact = "HBL 1234567890123"
                }),

            NewUser("Nadia Rehman", "owner.new@roomraah.local", "+923001110004", hash, UserRole.Owner, now,
                owner: new OwnerProfile
                {
                    CnicNumber = "3520112345672",
                    IdentityStatus = VerificationStatus.NotChecked,
                    PayoutContact = "Meezan 9876543210987"
                }),

            NewUser("Tariq Javed", "owner.failed@roomraah.local", "+923001110005", hash, UserRole.Owner, now,
                owner: new OwnerProfile
                {
                    CnicNumber = "3520112345673",
                    IdentityStatus = VerificationStatus.Failed,
                    IdentityCheckedAt = now.AddDays(-5),
                    PayoutContact = "UBL 5555666677778"
                }),

            NewUser("Hamza Iqbal", "seeker1@roomraah.local", "+923001110006", hash, UserRole.Seeker, now,
                seeker: new SeekerProfile
                {
                    MinBudget = 10000m,
                    MaxBudget = 25000m,
                    GenderPreference = GenderPolicy.Boys,
                    PreferredCityId = lahoreId,
                    PreferredLandmarkId = punjabUniversityId
                }),

            NewUser("Zainab Farooq", "seeker2@roomraah.local", "+923001110007", hash, UserRole.Seeker, now,
                seeker: new SeekerProfile
                {
                    MinBudget = 15000m,
                    MaxBudget = 40000m,
                    GenderPreference = GenderPolicy.Girls,
                    PreferredCityId = islamabadId,
                    PreferredLandmarkId = nustId
                }),

            NewUser("Usman Shahid", "seeker3@roomraah.local", "+923001110008", hash, UserRole.Seeker, now,
                seeker: new SeekerProfile()),

            NewUser("Maryam Aslam", "seeker.unverified@roomraah.local", "+923001110009", hash, UserRole.Seeker, now,
                seeker: new SeekerProfile(), isEmailVerified: false)
        };

        context.AppUsers.AddRange(users);
        await context.SaveChangesAsync();
    }

    private static async Task<int?> CityIdAsync(RoomRaahDbContext context, string name)
    {
        return (await context.Cities.FirstOrDefaultAsync(c => c.Name == name))?.Id;
    }

    private static async Task<int?> LandmarkIdAsync(RoomRaahDbContext context, string name)
    {
        return (await context.Landmarks.FirstOrDefaultAsync(l => l.Name == name))?.Id;
    }

    private static AppUser NewUser(
        string fullName,
        string email,
        string phoneNumber,
        string passwordHash,
        UserRole role,
        DateTime createdAt,
        SeekerProfile? seeker = null,
        OwnerProfile? owner = null,
        AdminProfile? admin = null,
        bool isEmailVerified = true)
    {
        return new AppUser
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
            PasswordHash = passwordHash,
            Role = role,
            IsEmailVerified = isEmailVerified,
            IsActive = true,
            CreatedAt = createdAt,
            SeekerProfile = seeker,
            OwnerProfile = owner,
            AdminProfile = admin
        };
    }
}
