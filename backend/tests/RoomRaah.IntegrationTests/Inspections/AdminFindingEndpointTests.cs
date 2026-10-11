using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Inspections;

/// <summary>Rules 135 to 138, end to end.</summary>
[Collection(ApiCollection.Name)]
public class AdminFindingEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public AdminFindingEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(OwnerWorkspace Owner, AdminWorkspace Admin, OwnerPropertyDto Listing)>
        PublishedAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);

        return (owner, admin, listing);
    }

    private static Task<HttpResponseMessage> RecordAsync(AdminWorkspace admin, int propertyId) =>
        admin.Client.PostJsonAsync("/api/v1/admin/inspections", new
        {
            PropertyId = propertyId,
            InspectedAt = DateTime.UtcNow.AddSeconds(-5),
            Result = "Passed",
            Notes = "Rooms and facilities matched the listing."
        });

    /// <summary>Every page of the requested worklist, so a test does not depend on its position.</summary>
    private static async Task<List<RequestedInspectionDto>> AllRequestedAsync(AdminWorkspace admin)
    {
        var rows = new List<RequestedInspectionDto>();
        for (var page = 1; ; page++)
        {
            var result = await (await admin.Client.GetAsync(
                    $"/api/v1/admin/inspections/requested?page={page}&pageSize=50"))
                .ReadAsync<PagedResult<RequestedInspectionDto>>();

            rows.AddRange(result.Items);
            if (rows.Count >= result.TotalCount || result.Items.Count == 0) return rows;
        }
    }

    // --------------------------------------------------------------- rule 135

    [Fact]
    public async Task Rule_135_a_search_returns_only_listings_whose_title_city_or_owner_matches()
    {
        var (_, admin, listing) = await PublishedAsync("find-title");

        var page = await (await admin.Client.GetAsync(
                "/api/v1/admin/properties?status=Published&q=INTEGRATION%20fixture&pageSize=50"))
            .ReadAsync<PagedResult<AdminPropertySummaryDto>>();

        page.TotalCount.ShouldBeGreaterThan(0);
        page.Items.ShouldAllBe(row =>
            row.Title.Contains("integration fixture", StringComparison.OrdinalIgnoreCase) ||
            row.CityName.Contains("integration fixture", StringComparison.OrdinalIgnoreCase) ||
            row.OwnerName.Contains("integration fixture", StringComparison.OrdinalIgnoreCase));
        page.Items.ShouldAllBe(row => row.Status == "Published");

        var byCity = await (await admin.Client.GetAsync(
                $"/api/v1/admin/properties?status=Published&q={Uri.EscapeDataString(listing.CityName)}&pageSize=50"))
            .ReadAsync<PagedResult<AdminPropertySummaryDto>>();

        byCity.TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Rule_135_a_search_that_matches_nothing_is_an_empty_page()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        var page = await (await admin.Client.GetAsync(
                "/api/v1/admin/properties?q=qqzzxx-no-such-listing"))
            .ReadAsync<PagedResult<AdminPropertySummaryDto>>();

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rule_135_a_one_character_search_is_refused()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        (await admin.Client.GetAsync("/api/v1/admin/properties?q=a"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // --------------------------------------------------------------- rule 136

    [Fact]
    public async Task Rule_136_an_owner_request_is_on_the_worklist_until_the_visit_is_recorded()
    {
        var (owner, admin, listing) = await PublishedAsync("requested-list");

        (await AllRequestedAsync(admin)).ShouldNotContain(row => row.PropertyId == listing.Id);

        (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .EnsureSuccessStatusCode();

        var row = (await AllRequestedAsync(admin)).Where(r => r.PropertyId == listing.Id).ShouldHaveSingleItem();
        row.HasInspectionBadge.ShouldBeFalse();
        row.LastInspectionAt.ShouldBeNull();

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        (await AllRequestedAsync(admin)).ShouldNotContain(r => r.PropertyId == listing.Id);
    }

    [Fact]
    public async Task Rule_136_only_an_admin_reaches_the_worklist()
    {
        var (owner, _, _) = await PublishedAsync("requested-owner");

        (await owner.Client.GetAsync("/api/v1/admin/inspections/requested"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --------------------------------------------------------------- rule 137

    [Fact]
    public async Task Rule_137_a_deactivated_account_is_told_why_and_only_with_the_right_password()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "deactivated-reason");
        var id = await IdOfAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/users/{id}/deactivate",
                new { Reason = "Repeated abusive messages." }))
            .EnsureSuccessStatusCode();

        var anonymous = _factory.CreateClient();

        var right = await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { Email = seeker.Email, Password = "Password1" });
        right.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await right.Content.ReadAsStringAsync())
            .ShouldContain("This account has been deactivated: Repeated abusive messages.");

        var wrong = await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { Email = seeker.Email, Password = "NotThePassword1" });
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrong.Content.ReadAsStringAsync()).ShouldNotContain("abusive");

        var detail = await (await admin.Client.GetAsync($"/api/v1/admin/users/{id}"))
            .ReadAsync<AdminUserDto>();
        detail.DeactivationReason.ShouldBe("Repeated abusive messages.");

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/users/{id}/reactivate",
                new { Reason = "The report was dismissed." }))
            .EnsureSuccessStatusCode();

        var after = await (await admin.Client.GetAsync($"/api/v1/admin/users/{id}"))
            .ReadAsync<AdminUserDto>();
        after.DeactivationReason.ShouldBeNull();
    }

    [Fact]
    public async Task Rule_137_a_reason_too_long_to_keep_is_refused()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "deactivated-long");
        var id = await IdOfAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/users/{id}/deactivate",
                new { Reason = new string('x', 1001) }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<int> IdOfAsync(AdminWorkspace admin, string email)
    {
        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        return page.Items.Single(u => u.Email == email).Id;
    }

    // --------------------------------------------------------------- rule 138

    [Fact]
    public async Task Rule_138_the_owner_sees_why_a_badge_came_off_until_it_is_granted_again()
    {
        var (owner, admin, listing) = await PublishedAsync("badge-reason");

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();
        (await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/badge/grant",
                new { Reason = "Passed the visit." }))
            .EnsureSuccessStatusCode();

        (await RowAsync(owner, listing.Id)).BadgeRemovedReason.ShouldBeNull();

        (await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/badge/remove",
                new { Reason = "A report about hot water was upheld." }))
            .EnsureSuccessStatusCode();

        var removed = await RowAsync(owner, listing.Id);
        removed.HasInspectionBadge.ShouldBeFalse();
        removed.BadgeRemovedReason.ShouldBe("A report about hot water was upheld.");
        removed.BadgeRemovedAt.ShouldNotBeNull();

        (await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/badge/grant",
                new { Reason = "Fixed and checked again." }))
            .EnsureSuccessStatusCode();

        var regranted = await RowAsync(owner, listing.Id);
        regranted.HasInspectionBadge.ShouldBeTrue();
        regranted.BadgeRemovedReason.ShouldBeNull();
        regranted.BadgeRemovedAt.ShouldBeNull();
    }

    private static async Task<OwnerPropertySummaryDto> RowAsync(OwnerWorkspace owner, int listingId)
    {
        var page = await (await owner.Client.GetAsync("/api/v1/owner/properties?pageSize=50"))
            .ReadAsync<PagedResult<OwnerPropertySummaryDto>>();

        return page.Items.Single(row => row.Id == listingId);
    }
}
