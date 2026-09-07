using System.Net.Http.Json;
using RoomRaah.Application.DTOs;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// A freshly registered, verified owner and the client that acts as them. Every
/// test gets its own account, so one test's listings are never another's fixture.
/// </summary>
public class OwnerWorkspace
{
    public required HttpClient Client { get; init; }

    /// <summary>The address this owner registered with, for the privacy assertions.</summary>
    public required string Email { get; init; }

    /// <summary>
    /// The bearer token this account holds. The HttpClient already carries it; the hub
    /// needs it separately, because a WebSocket handshake cannot set a header.
    /// </summary>
    public required string AccessToken { get; init; }

    public static async Task<OwnerWorkspace> CreateAsync(RoomRaahApiFactory factory, string label)
    {
        var client = factory.CreateClient();

        var email = $"owner.{label}.{Guid.NewGuid():N}@roomraah.local";

        var session = await client.RegisterAndVerifyAsync(factory, email, "Owner");

        client.Authorise(session.AccessToken);

        return new OwnerWorkspace
        {
            Client = client,
            Email = email,
            AccessToken = session.AccessToken
        };
    }

    public static async Task<int> AnyAreaIdAsync(HttpClient client)
    {
        var cities = await (await client.GetAsync("/api/v1/locations/cities"))
            .ReadAsync<List<CityDto>>();

        var areas = await (await client.GetAsync($"/api/v1/locations/cities/{cities[0].Id}/areas"))
            .ReadAsync<List<AreaDto>>();

        return areas[0].Id;
    }

    public static async Task<List<FacilityDto>> FacilitiesAsync(HttpClient client)
    {
        return await (await client.GetAsync("/api/v1/facilities")).ReadAsync<List<FacilityDto>>();
    }

    /// <summary>
    /// The title every listing a test creates carries, and it must not be one the
    /// seeder uses. The seed assertions tell their own listings apart from a test's
    /// by title, and while this fixture borrowed a seeded one those assertions
    /// counted the fixtures too — quietly, until enough tests existed to push the
    /// count past what they expected. A test guards the difference.
    /// </summary>
    public const string FixtureTitle = "Integration fixture room off Main Boulevard";

    /// <summary>
    /// And its own point on the map, for exactly the same reason.
    ///
    /// This fixture was copied from the seeder and kept the seeded Gulberg listing's
    /// coordinates, so every fixture in the suite sat on top of it. The distance test
    /// passed by coincidence — any Gulberg card was the right distance from the
    /// University of the Punjab, seeded or not — which meant it was not testing what
    /// its name said. 5.6208 km from that landmark, where the seeded one is 6.1963.
    /// </summary>
    public const double FixtureLatitude = 31.5400;

    public const double FixtureLongitude = 74.3300;

    /// <summary>The distance from the University of the Punjab, rounded as rule 80 rounds it.</summary>
    public const double FixtureDistanceFromPunjabUniversityKm = 5.6;

    public static object Listing(int areaId, params int[] facilityIds) => new
    {
        AreaId = areaId,
        Title = FixtureTitle,
        Description = "A four-bed shared room in a well-kept portion, ten minutes from Main Boulevard.",
        AddressLine = "House 42, Block C, Gulberg III",
        Latitude = FixtureLatitude,
        Longitude = FixtureLongitude,
        RoomType = "Shared",
        GenderPolicy = "Boys",
        TotalBeds = 4,
        AvailableBeds = 2,
        MonthlyRent = 18000,
        SecurityDeposit = 18000,
        UtilitiesCharge = 2500,
        MessCharge = 8000,
        HouseRules = "No smoking indoors.",
        FacilityIds = facilityIds
    };

    public async Task<OwnerPropertyDto> CreateDraftAsync(int? areaId = null, params int[] facilityIds)
    {
        var area = areaId ?? await AnyAreaIdAsync(Client);

        var response = await Client.PostJsonAsync("/api/v1/owner/properties", Listing(area, facilityIds));

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<OwnerPropertyDto>();
    }

    public async Task<PropertyPhotoDto> UploadAsync(int propertyId, byte[]? bytes = null,
        string fileName = "room.jpg", string contentType = "image/jpeg")
    {
        using var content = TestImages.AsUpload(bytes ?? TestImages.Jpeg(), fileName, contentType);

        var response = await Client.PostAsync($"/api/v1/owner/properties/{propertyId}/photos", content);

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<PropertyPhotoDto>();
    }

    /// <summary>A listing with the three photographs rule 15 asks for.</summary>
    public async Task<OwnerPropertyDto> CreateSubmittableAsync()
    {
        var listing = await CreateDraftAsync();

        for (var i = 0; i < 3; i++)
        {
            await UploadAsync(listing.Id);
        }

        return listing;
    }

    /// <summary>A listing that has been through the whole journey and is live.</summary>
    public async Task<OwnerPropertyDto> CreatePublishedAsync(AdminWorkspace admin)
    {
        var listing = await CreateSubmittableAsync();

        (await Client.PostJsonAsync($"/api/v1/owner/properties/{listing.Id}/submit", new { }))
            .EnsureSuccessStatusCode();

        await admin.PublishAsync(listing.Id);

        return await (await Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();
    }

    /// <summary>
    /// The listing exactly as it stands, ready for a test to change one field. An
    /// update sends every field, so building it from the listing is what keeps a
    /// test about the rent from also silently proposing a new address.
    /// </summary>
    public static object EditOf(OwnerPropertyDto listing, Action<Dictionary<string, object?>>? change = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["areaId"] = listing.AreaId,
            ["title"] = listing.Title,
            ["description"] = listing.Description,
            ["addressLine"] = listing.AddressLine,
            ["latitude"] = listing.Latitude,
            ["longitude"] = listing.Longitude,
            ["roomType"] = listing.RoomType,
            ["genderPolicy"] = listing.GenderPolicy,
            ["totalBeds"] = listing.TotalBeds,
            ["availableBeds"] = listing.AvailableBeds,
            ["monthlyRent"] = listing.MonthlyRent,
            ["securityDeposit"] = listing.SecurityDeposit,
            ["utilitiesCharge"] = listing.UtilitiesCharge,
            ["messCharge"] = listing.MessCharge,
            ["houseRules"] = listing.HouseRules,
            ["facilityIds"] = listing.Facilities.Select(f => f.Id).ToList()
        };

        change?.Invoke(fields);

        return fields;
    }
}
