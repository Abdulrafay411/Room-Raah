using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// A client signed in as the seeded admin. Admins are not registered through the
/// public endpoint — the product has no route that creates one — so the review tests
/// sign in as the account the seeder writes.
/// </summary>
public class AdminWorkspace
{
    /// <summary>The seeded admin who makes physical inspections.</summary>
    public const string Email = "admin@roomraah.local";

    /// <summary>
    /// The seeded admin who does not. The seeder has one of each on purpose: "admin"
    /// and "admin who makes visits" are different answers, and rules 109 and 110 are
    /// only exercised if a test can be both.
    /// </summary>
    public const string NonInspectorEmail = "admin2@roomraah.local";

    public required HttpClient Client { get; init; }

    public static Task<AdminWorkspace> CreateAsync(RoomRaahApiFactory factory) =>
        SignInAsync(factory, Email);

    /// <summary>An admin whose <c>CanInspect</c> is false.</summary>
    public static Task<AdminWorkspace> CreateNonInspectorAsync(RoomRaahApiFactory factory) =>
        SignInAsync(factory, NonInspectorEmail);

    private static async Task<AdminWorkspace> SignInAsync(RoomRaahApiFactory factory, string email)
    {
        var client = factory.CreateClient();

        var login = await client.PostJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "Password1"
        });

        login.EnsureSuccessStatusCode();

        client.Authorise((await login.ReadAsync<AuthResponseDto>()).AccessToken);

        return new AdminWorkspace { Client = client };
    }

    /// <summary>Records the four checks rule 23 gates publishing on.</summary>
    public async Task PassEveryCheckAsync(int propertyId)
    {
        foreach (var type in new[]
                 {
                     VerificationCheckType.OwnerIdentity,
                     VerificationCheckType.Address,
                     VerificationCheckType.Photos,
                     VerificationCheckType.Facilities
                 })
        {
            var response = await Client.PostJsonAsync(
                $"/api/v1/admin/properties/{propertyId}/verify",
                new { CheckType = type.ToString(), Status = "Checked" });

            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Takes a listing all the way from submitted to published.</summary>
    public async Task<AdminPropertyDto> PublishAsync(int propertyId)
    {
        await PassEveryCheckAsync(propertyId);

        var response = await Client.PostJsonAsync(
            $"/api/v1/admin/properties/{propertyId}/approve", new { });

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<AdminPropertyDto>();
    }
}
