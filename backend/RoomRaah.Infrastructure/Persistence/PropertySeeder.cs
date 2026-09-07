using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using SkiaSharp;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Development listings, shaped so every status, both ends of the availability
/// rules and the inspection badge are all visible without touching the database by
/// hand. Runs only when there are no listings at all.
/// </summary>
public static class PropertySeeder
{
    private const int PhotosPerListing = 4;
    private const int StaleDays = 45;

    private record Listing(
        string OwnerEmail,
        string AreaName,
        string Title,
        string Description,
        string AddressLine,
        double Latitude,
        double Longitude,
        RoomType RoomType,
        GenderPolicy GenderPolicy,
        int TotalBeds,
        int AvailableBeds,
        decimal MonthlyRent,
        decimal SecurityDeposit,
        decimal UtilitiesCharge,
        decimal MessCharge,
        string? HouseRules,
        PropertyStatus Status,
        bool Badge,
        int ConfirmedDaysAgo,
        string[] Facilities);

    private const string CheckedOwner = "owner.checked@roomraah.local";
    private const string NewOwner = "owner.new@roomraah.local";
    private const string FailedOwner = "owner.failed@roomraah.local";

    private static readonly Listing[] Listings =
    [
        // Eight published listings, all owned by the only owner whose identity has
        // been checked: rule 23 will not let anybody else reach Published.
        new(CheckedOwner, "Gulberg", "Bright shared room near Main Boulevard",
            "A four-bed shared room in a well-kept portion, ten minutes from Main Boulevard. Attached bath, study desk per bed, and a separate entrance for tenants.",
            "House 42, Block C, Gulberg III", 31.5204, 74.3587,
            RoomType.Shared, GenderPolicy.Boys, 4, 2, 18000m, 18000m, 2500m, 8000m,
            "No smoking indoors. Guests until 10pm.",
            PropertyStatus.Published, true, 2, ["Wi-Fi", "Mess", "Air Conditioning", "Security"]),

        new(CheckedOwner, "Johar Town", "Private room for students, Johar Town",
            "Furnished private room in a family home in Johar Town, close to the Emporium side. Includes a wardrobe, a study table and a private bathroom.",
            "House 7, Block G1, Johar Town", 31.4697, 74.2728,
            RoomType.Private, GenderPolicy.Girls, 2, 1, 25000m, 25000m, 3000m, 9000m,
            "Female tenants only. Quiet hours after 11pm.",
            PropertyStatus.Published, true, 3, ["Wi-Fi", "Mess", "Air Conditioning", "Laundry", "Security"]),

        new(CheckedOwner, "Model Town", "Six-bed hostel room, Model Town",
            "Budget six-bed room in an established boys hostel. Shared bathroom on the same floor, common study area downstairs, and a mess that runs three meals a day.",
            "House 118, Block B, Model Town", 31.4805, 74.3239,
            RoomType.Shared, GenderPolicy.Boys, 6, 0, 14000m, 10000m, 2000m, 7500m,
            "No cooking in rooms.",
            PropertyStatus.Published, false, 5, ["Wi-Fi", "Mess", "Electricity Backup"]),

        new(CheckedOwner, "Clifton", "Single room with sea breeze, Clifton",
            "A single private room in a quiet Clifton apartment, walking distance from the beach side. Suits a working professional who wants their own space.",
            "Flat 5B, Block 2, Clifton", 24.8138, 67.0300,
            RoomType.Private, GenderPolicy.Girls, 1, 1, 32000m, 32000m, 4000m, 0m,
            "No overnight guests.",
            PropertyStatus.Published, true, 1, ["Wi-Fi", "Air Conditioning", "Laundry", "Parking", "Security"]),

        new(CheckedOwner, "Gulshan-e-Iqbal", "Shared room near NED University",
            "Five-bed shared room a short ride from NED University. Filtered water, generator backup, and a caretaker on site through the week.",
            "House 61, Block 13-D, Gulshan-e-Iqbal", 24.9265, 67.0972,
            RoomType.Shared, GenderPolicy.Boys, 5, 3, 16000m, 12000m, 2200m, 7000m,
            null,
            PropertyStatus.Published, false, StaleDays, ["Wi-Fi", "Mess", "Electricity Backup", "Security"]),

        new(CheckedOwner, "North Nazimabad", "Family portion, North Nazimabad",
            "Independent upper portion suitable for a small family. Three rooms, its own kitchen, and a separate stair entrance from the street.",
            "House 24, Block H, North Nazimabad", 24.9425, 67.0356,
            RoomType.Private, GenderPolicy.Family, 3, 3, 28000m, 28000m, 3500m, 0m,
            "Family tenants only.",
            PropertyStatus.Published, false, StaleDays + 5, ["Wi-Fi", "Parking", "Electricity Backup"]),

        new(CheckedOwner, "F-7", "Girls shared accommodation, F-7",
            "Four-bed room in a serviced girls hostel in F-7 Markaz. Housekeeping twice a week, laundry on site, and a warden who lives on the premises.",
            "House 12, Street 26, F-7/2", 33.7180, 73.0551,
            RoomType.Shared, GenderPolicy.Girls, 4, 0, 22000m, 20000m, 3000m, 8500m,
            "Entry closes at 10pm.",
            PropertyStatus.Published, false, 4, ["Wi-Fi", "Mess", "Laundry", "Security", "Air Conditioning"]),

        new(CheckedOwner, "G-11", "Private room in G-11, near metro",
            "A private room with an attached bath in a shared house in G-11. Two minutes from the metro stop, which makes most of the city reachable without a car.",
            "House 340, Street 44, G-11/3", 33.6680, 72.9930,
            RoomType.Private, GenderPolicy.Boys, 2, 2, 30000m, 30000m, 3500m, 0m,
            null,
            PropertyStatus.Published, false, 6, ["Wi-Fi", "Air Conditioning", "Parking"]),

        // Two waiting in the review queue.
        new(NewOwner, "I-8", "Two-bed room near NUST shuttle stop",
            "A two-bed room in I-8, on the route the NUST shuttle takes in the morning. Recently painted, with new mattresses and a shared kitchen.",
            "House 88, Street 12, I-8/4", 33.6650, 73.0750,
            RoomType.Shared, GenderPolicy.Boys, 2, 2, 20000m, 18000m, 2500m, 6500m,
            null,
            PropertyStatus.PendingReview, false, 0, ["Wi-Fi", "Mess", "Electricity Backup"]),

        new(NewOwner, "Gulberg", "Studio portion in Gulberg II",
            "A self-contained studio portion with its own bath and a small kitchenette. Suits one person who would rather not share a kitchen.",
            "House 9, Block K, Gulberg II", 31.5100, 74.3450,
            RoomType.Private, GenderPolicy.Girls, 1, 1, 27000m, 27000m, 3000m, 0m,
            "No pets.",
            PropertyStatus.PendingReview, false, 0, ["Wi-Fi", "Air Conditioning", "Laundry"]),

        // One the admin has sent back.
        new(NewOwner, "Johar Town", "Rooms available near UET campus",
            "Shared rooms in a purpose-built hostel a short walk from the UET side of Johar Town. Common room with a television and a rooftop study area.",
            "House 55, Block J3, Johar Town", 31.4630, 74.2810,
            RoomType.Shared, GenderPolicy.Boys, 8, 5, 13000m, 9000m, 1800m, 6800m,
            null,
            PropertyStatus.ChangesRequested, false, 0, ["Wi-Fi", "Mess"]),

        // One that was turned down.
        new(FailedOwner, "Clifton", "Rooms to let, Clifton block 5",
            "Rooms available in a Clifton building close to the commercial area. Contact for details of the terms and the available dates.",
            "Flat 12, Block 5, Clifton", 24.8210, 67.0290,
            RoomType.Shared, GenderPolicy.Boys, 4, 4, 24000m, 20000m, 3000m, 7000m,
            null,
            PropertyStatus.Rejected, false, 0, ["Wi-Fi"]),

        // One the owner has not finished.
        new(NewOwner, "Model Town", "Room in Model Town, work in progress",
            "A private room in a Model Town house. The listing is still being written, and the photographs are the ones taken before the room was repainted.",
            "House 200, Block D, Model Town", 31.4840, 74.3300,
            RoomType.Private, GenderPolicy.Boys, 1, 1, 21000m, 21000m, 2500m, 0m,
            null,
            PropertyStatus.Draft, false, 0, ["Wi-Fi", "Parking"]),

        // One the owner has taken down.
        new(CheckedOwner, "G-11", "Shared room, G-11 (currently taken)",
            "A three-bed shared room in G-11 that is fully occupied at the moment. The listing is kept so it can be brought back when a bed frees up.",
            "House 15, Street 8, G-11/1", 33.6700, 72.9970,
            RoomType.Shared, GenderPolicy.Boys, 3, 0, 17000m, 15000m, 2200m, 7200m,
            null,
            PropertyStatus.Unpublished, false, 20, ["Wi-Fi", "Mess", "Security"])
    ];

    /// <summary>
    /// Listings and their photographs are guarded separately. They are written in
    /// two saves, and a failure between them used to leave the database with
    /// listings and no photographs for good: one guard over both steps sees the
    /// listings, concludes the seed has run, and never repairs the half it missed.
    /// </summary>
    /// <summary>
    /// The titles this seeder writes. Both guards below are scoped to them, so a
    /// listing an owner created is never mistaken for seed data and never has
    /// placeholder photographs added to it.
    /// </summary>
    public static IReadOnlyList<string> SeededTitles { get; } =
        Listings.Select(l => l.Title).ToArray();

    public static async Task SeedAsync(RoomRaahDbContext context, IPhotoStorageService photoStorage)
    {
        var listings = await EnsureListingsAsync(context);

        if (listings.Count == 0)
        {
            return;
        }

        // Three steps, three guards. One guard over all of them sees the first step
        // and concludes the seed has run, and the rest is never repaired.
        await EnsurePhotosAsync(context, photoStorage, listings);
        await EnsureChecksAsync(context, listings);
        await EnsureRevisionsAsync(context, listings);
    }

    /// <summary>
    /// Rule 23 needs four passing checks before a listing can be published, so every
    /// published listing carries them. The two waiting for review are shaped to show
    /// both answers: one an admin can approve on sight, and one that will be refused
    /// until the last check is recorded.
    /// </summary>
    private static async Task EnsureChecksAsync(RoomRaahDbContext context, List<Property> listings)
    {
        var ids = listings.Select(l => l.Id).ToList();

        if (await context.VerificationChecks.IgnoreQueryFilters().AnyAsync(c => ids.Contains(c.PropertyId)))
        {
            return;
        }

        var admin = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var pendingSeen = 0;

        foreach (var property in listings)
        {
            var types = ListingReview.RequiredToPublish.ToList();

            if (property.Status == PropertyStatus.PendingReview && pendingSeen++ == 1)
            {
                // The second one waiting for review is missing its last check, so the
                // approve endpoint has something real to refuse.
                types.RemoveAt(types.Count - 1);
            }
            else if (property.Status is PropertyStatus.Draft or PropertyStatus.Rejected)
            {
                types = [VerificationCheckType.OwnerIdentity];
            }

            foreach (var type in types)
            {
                context.VerificationChecks.Add(new VerificationCheck
                {
                    PropertyId = property.Id,
                    CheckType = type,
                    Status = property.Status == PropertyStatus.Rejected && type == VerificationCheckType.OwnerIdentity
                        ? VerificationStatus.Failed
                        : VerificationStatus.Checked,
                    EvidenceDate = now.AddDays(-14),
                    ReviewedByAdminId = admin,
                    RecordedAt = now.AddDays(-14)
                });
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// One of each kind, so both review queues have something in them on a fresh
    /// database and the split between them is visible without an owner having to
    /// submit anything first.
    /// </summary>
    private static async Task EnsureRevisionsAsync(RoomRaahDbContext context, List<Property> listings)
    {
        var ids = listings.Select(l => l.Id).ToList();

        if (await context.PropertyRevisions.IgnoreQueryFilters().AnyAsync(r => ids.Contains(r.PropertyId)))
        {
            return;
        }

        var published = listings.Where(l => l.Status == PropertyStatus.Published).Take(2).ToList();

        if (published.Count < 2)
        {
            return;
        }

        var content = ListingRevisions.Snapshot(published[0], RevisionKind.Content);
        content.MonthlyRent = published[0].MonthlyRent + 2000m;
        content.Description = published[0].Description + " Newly repainted, with a second study desk.";
        content.SubmittedAt = DateTime.UtcNow.AddDays(-3);

        var availability = ListingRevisions.Snapshot(published[1], RevisionKind.Availability);
        availability.AvailableBeds = Math.Max(0, published[1].AvailableBeds - 1);
        availability.SubmittedAt = DateTime.UtcNow.AddHours(-6);

        context.PropertyRevisions.AddRange(content, availability);

        await context.SaveChangesAsync();
    }

    private static async Task<List<Property>> EnsureListingsAsync(RoomRaahDbContext context)
    {
        var existing = await context.Properties
            .IgnoreQueryFilters()
            .Where(p => SeededTitles.Contains(p.Title))
            // Ordered by id, because the placeholder colour is derived from a
            // listing's position and a re-run has to reproduce the same one.
            .OrderBy(p => p.Id)
            .ToListAsync();

        if (existing.Count > 0)
        {
            return existing;
        }

        var owners = await context.AppUsers
            .IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Owner)
            .ToDictionaryAsync(u => u.Email, u => u.Id);

        var areas = await context.Areas.ToDictionaryAsync(a => a.Name, a => a.Id);
        var facilities = await context.Facilities.ToDictionaryAsync(f => f.Name, f => f.Id);

        if (owners.Count == 0 || areas.Count == 0 || facilities.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var created = new List<Property>();

        foreach (var listing in Listings)
        {
            var property = new Property
            {
                OwnerId = owners[listing.OwnerEmail],
                AreaId = areas[listing.AreaName],
                Title = listing.Title,
                Description = listing.Description,
                AddressLine = listing.AddressLine,
                Latitude = listing.Latitude,
                Longitude = listing.Longitude,
                RoomType = listing.RoomType,
                GenderPolicy = listing.GenderPolicy,
                TotalBeds = listing.TotalBeds,
                AvailableBeds = listing.AvailableBeds,
                MonthlyRent = listing.MonthlyRent,
                SecurityDeposit = listing.SecurityDeposit,
                UtilitiesCharge = listing.UtilitiesCharge,
                MessCharge = listing.MessCharge,
                HouseRules = listing.HouseRules,
                Status = listing.Status,
                HasInspectionBadge = listing.Badge,
                LastInspectionAt = listing.Badge ? now.AddDays(-10) : null,
                AvailabilityConfirmedAt = now.AddDays(-listing.ConfirmedDaysAgo),
                IsActive = true,
                CreatedAt = now.AddDays(-listing.ConfirmedDaysAgo - 7),
                UpdatedAt = now.AddDays(-listing.ConfirmedDaysAgo),
                Facilities = listing.Facilities
                    .Select(name => new PropertyFacility { FacilityId = facilities[name] })
                    .ToList()
            };

            context.Properties.Add(property);
            created.Add(property);
        }

        await context.SaveChangesAsync();

        return created;
    }

    /// <summary>
    /// Photographs go through the same upload path a real one does, so a seeded
    /// listing shows a real image and a real thumbnail rather than a URL that
    /// resolves to nothing. Fake data here would make every later phase harder to
    /// judge, which is the reason the rest of the seed is real too.
    /// </summary>
    private static async Task EnsurePhotosAsync(
        RoomRaahDbContext context, IPhotoStorageService photoStorage, List<Property> listings)
    {
        var ids = listings.Select(l => l.Id).ToList();

        if (await context.PropertyPhotos.IgnoreQueryFilters().AnyAsync(p => ids.Contains(p.PropertyId)))
        {
            return;
        }

        for (var index = 0; index < listings.Count; index++)
        {
            var property = listings[index];

            for (var position = 1; position <= PhotosPerListing; position++)
            {
                using var image = Placeholder(index, position);

                var stored = await photoStorage.UploadAsync(
                    property.Id, image, $"seed_{property.Id}_{position}.jpg");

                if (stored is null)
                {
                    continue;
                }

                context.PropertyPhotos.Add(new PropertyPhoto
                {
                    PropertyId = property.Id,
                    BlobUrl = stored.BlobUrl,
                    ThumbnailUrl = stored.ThumbnailUrl,
                    IsPrimary = position == 1,
                    SortOrder = position,
                    Status = PhotoStatus.Live,
                    UploadedAt = property.CreatedAt
                });
            }
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A real JPEG, drawn rather than downloaded: no binary files in the repository,
    /// no network call at startup, and every listing recognisably its own colour.
    /// </summary>
    private static MemoryStream Placeholder(int listingIndex, int position)
    {
        const int width = 1000;
        const int height = 750;

        var hue = listingIndex * 360f / Listings.Length;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        canvas.Clear(SKColor.FromHsl(hue, 45f, 78f));

        using var band = new SKPaint { Color = SKColor.FromHsl(hue, 55f, 55f), IsAntialias = true };
        canvas.DrawRect(new SKRect(0, height * 0.62f, width, height), band);

        using var marker = new SKPaint { Color = SKColor.FromHsl(hue, 65f, 32f), IsAntialias = true };
        for (var i = 0; i < position; i++)
        {
            canvas.DrawCircle(90 + i * 130, 130, 46, marker);
        }

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);

        var output = new MemoryStream();
        encoded.SaveTo(output);
        output.Position = 0;

        return output;
    }
}
