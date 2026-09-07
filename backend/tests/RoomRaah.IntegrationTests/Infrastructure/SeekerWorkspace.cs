using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.Infrastructure.Persistence;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// A freshly registered, verified seeker and the client that acts as them. Every
/// test gets its own account, so one test's shortlist is never another's fixture.
/// </summary>
public class SeekerWorkspace
{
    public required HttpClient Client { get; init; }

    /// <summary>The address this seeker registered with, for the privacy assertions.</summary>
    public required string Email { get; init; }

    /// <summary>
    /// The bearer token this account holds. The HttpClient already carries it; the hub
    /// needs it separately, because a WebSocket handshake cannot set a header.
    /// </summary>
    public required string AccessToken { get; init; }

    public static async Task<SeekerWorkspace> CreateAsync(RoomRaahApiFactory factory, string label)
    {
        var client = factory.CreateClient();

        var email = $"seeker.{label}.{Guid.NewGuid():N}@roomraah.local";

        var session = await client.RegisterAndVerifyAsync(factory, email);

        client.Authorise(session.AccessToken);

        return new SeekerWorkspace
        {
            Client = client,
            Email = email,
            AccessToken = session.AccessToken
        };
    }

    public async Task<VisitRequestDto> RequestVisitAsync(
        int propertyId, string visitType = "Physical", DateTime? preferredAt = null)
    {
        var response = await Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = propertyId,
            VisitType = visitType,
            PreferredAt = preferredAt ?? DateTime.UtcNow.AddDays(3)
        });

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<VisitRequestDto>();
    }

    /// <summary>
    /// Moves a visit's preferred time into the past.
    ///
    /// This is not a way around a rule, it is the passage of time. Rule 90 will not
    /// complete a visit whose time has not come, and the validator will not accept a
    /// request for a time that has already gone — so nothing a test can do through
    /// the API alone produces a visit old enough to complete. The seeder solves the
    /// same problem for the running product by dating one request two days ago.
    /// </summary>
    public static async Task BackdateAsync(RoomRaahApiFactory factory, int visitId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        var visit = await context.VisitRequests.IgnoreQueryFilters()
            .FirstAsync(v => v.Id == visitId);

        visit.PreferredAt = DateTime.UtcNow.AddDays(-1);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Takes a listing off the public site.
    ///
    /// Written against the database rather than through the API because §5 has no
    /// endpoint that sets <c>Unpublished</c> at all — a gap in the admin listing area,
    /// recorded in the documentation rather than closed in this phase. Rule 87 is
    /// about what happens to a shortlist afterwards, and it cannot be tested at all
    /// unless a listing can get there.
    /// </summary>
    public static async Task UnpublishAsync(RoomRaahApiFactory factory, int propertyId)
    {
        using var scope = factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

        var property = await context.Properties.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == propertyId);

        property.Status = PropertyStatus.Unpublished;

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The whole gate rule 38 asks about: a request, the owner accepting it, its time
    /// arriving, and somebody saying it happened.
    /// </summary>
    public async Task<VisitRequestDto> CompleteVisitAsync(
        RoomRaahApiFactory factory, OwnerWorkspace owner, int propertyId)
    {
        var visit = await RequestVisitAsync(propertyId);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        })).EnsureSuccessStatusCode();

        await BackdateAsync(factory, visit.Id);

        var completed = await Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/complete", new { });

        completed.EnsureSuccessStatusCode();

        return await completed.ReadAsync<VisitRequestDto>();
    }

    public async Task<MyReviewDto> ReviewAsync(
        int propertyId, int rating = 4, string stayStatus = "Visited")
    {
        var response = await Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = propertyId,
            Rating = rating,
            Comment = "Clean room and the mess food is better than I expected.",
            StayStatus = stayStatus
        });

        response.EnsureSuccessStatusCode();

        return await response.ReadAsync<MyReviewDto>();
    }
}
