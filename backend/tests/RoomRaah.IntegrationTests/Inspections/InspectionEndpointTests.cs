using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Inspections;

[Collection(ApiCollection.Name)]
public class InspectionEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public InspectionEndpointTests(RoomRaahApiFactory factory)
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

    private static Task<HttpResponseMessage> RecordAsync(
        AdminWorkspace admin, int propertyId, string result = "Passed", DateTime? at = null)
    {
        return admin.Client.PostJsonAsync("/api/v1/admin/inspections", new
        {
            PropertyId = propertyId,
            InspectedAt = at ?? DateTime.UtcNow.AddDays(-1),
            Result = result,
            Notes = "Rooms and facilities matched the listing.",
            FeeAmount = 1500m
        });
    }

    private static Task<HttpResponseMessage> GrantAsync(
        AdminWorkspace admin, int propertyId, string reason = "Passed the visit.")
    {
        return admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{propertyId}/badge/grant", new { Reason = reason });
    }

    // ------------------------------------------------------------- the journey

    [Fact]
    public async Task An_owner_asks_once_the_team_visits_and_the_badge_goes_on()
    {
        var (owner, admin, listing) = await PublishedAsync("inspect-journey");

        var requested = await (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .ReadAsync<OwnerInspectionStateDto>();

        requested.InspectionRequested.ShouldBeTrue();
        requested.HasInspectionBadge.ShouldBeFalse();

        var inspection = await (await RecordAsync(admin, listing.Id)).ReadAsync<PropertyInspectionDto>();

        inspection.Result.ShouldBe("Passed");

        var badged = await (await GrantAsync(admin, listing.Id)).ReadAsync<AdminPropertyDto>();

        badged.HasInspectionBadge.ShouldBeTrue();

        // Rule 113: the badge shows on every card, so a cached one would be a claim
        // RoomRaah had already stopped standing behind.
        var card = await (await owner.Client.GetAsync($"/api/v1/properties/{listing.Id}"))
            .ReadAsync<ListingDetailDto>();

        card.HasInspectionBadge.ShouldBeTrue();
    }

    // ------------------------------------------------------- request (rule 108)

    [Fact]
    public async Task An_owner_may_only_ask_once()
    {
        var (owner, _, listing) = await PublishedAsync("inspect-once");

        (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .EnsureSuccessStatusCode();

        var again = await owner.Client.PostJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Another_owner_cannot_ask_for_an_inspection_of_a_listing()
    {
        var (_, _, listing) = await PublishedAsync("inspect-other");
        var stranger = await OwnerWorkspace.CreateAsync(_factory, "inspect-stranger");

        (await stranger.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_draft_cannot_be_inspected()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "inspect-draft");
        var draft = await owner.CreateDraftAsync();

        (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{draft.Id}/inspection-request", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // -------------------------------------------------------- record (rule 109)

    /// <summary>
    /// Rule 109. The seeder has an admin of each kind so this is a real difference and
    /// not a substitute agreeing with the test that wrote it.
    /// </summary>
    [Fact]
    public async Task An_admin_who_does_not_make_visits_cannot_record_one()
    {
        var (_, _, listing) = await PublishedAsync("inspect-notinspector");
        var plain = await AdminWorkspace.CreateNonInspectorAsync(_factory);

        (await RecordAsync(plain, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_owner_cannot_record_an_inspection_of_their_own_listing()
    {
        var (owner, _, listing) = await PublishedAsync("inspect-owner-records");

        (await owner.Client.PostJsonAsync("/api/v1/admin/inspections", new
        {
            PropertyId = listing.Id,
            InspectedAt = DateTime.UtcNow.AddDays(-1),
            Result = "Passed"
        })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Rule 115: a visit that has not happened is not a record of a visit.</summary>
    [Fact]
    public async Task A_visit_cannot_be_recorded_for_a_date_that_has_not_come()
    {
        var (_, admin, listing) = await PublishedAsync("inspect-future");

        var response = await RecordAsync(admin, listing.Id, at: DateTime.UtcNow.AddDays(5));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.ReadErrorAsync();
        error.Errors.ShouldNotBeNull();
        error.Errors!.ShouldContain(e => e.Field == "InspectedAt");
    }

    // ---------------------------------------------------- badge (rules 110-113)

    [Fact]
    public async Task The_badge_is_refused_until_a_visit_has_passed()
    {
        var (_, admin, listing) = await PublishedAsync("badge-noinspection");

        (await GrantAsync(admin, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_failed_visit_does_not_earn_a_badge()
    {
        var (_, admin, listing) = await PublishedAsync("badge-failed");

        (await RecordAsync(admin, listing.Id, "Failed")).EnsureSuccessStatusCode();

        (await GrantAsync(admin, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// The latest verdict, not any verdict. A listing that passed and then failed has a
    /// passed inspection in its history and no business carrying a badge.
    /// </summary>
    [Fact]
    public async Task A_pass_followed_by_a_failure_does_not_earn_a_badge()
    {
        var (_, admin, listing) = await PublishedAsync("badge-then-failed");

        (await RecordAsync(admin, listing.Id, "Passed", DateTime.UtcNow.AddDays(-30)))
            .EnsureSuccessStatusCode();
        (await RecordAsync(admin, listing.Id, "Failed", DateTime.UtcNow.AddDays(-1)))
            .EnsureSuccessStatusCode();

        (await GrantAsync(admin, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_badge_cannot_be_granted_twice()
    {
        var (_, admin, listing) = await PublishedAsync("badge-twice");

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        (await GrantAsync(admin, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task An_admin_who_does_not_make_visits_cannot_grant_a_badge()
    {
        var (_, admin, listing) = await PublishedAsync("badge-notinspector");
        var plain = await AdminWorkspace.CreateNonInspectorAsync(_factory);

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        (await GrantAsync(plain, listing.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Rule 111. A claim being withdrawn should not wait for the one person who makes
    /// visits — an upheld report or a failed re-inspection is exactly when it must not.
    /// </summary>
    [Fact]
    public async Task Any_admin_may_take_a_badge_off()
    {
        var (_, admin, listing) = await PublishedAsync("badge-remove");
        var plain = await AdminWorkspace.CreateNonInspectorAsync(_factory);

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var removed = await (await plain.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/badge/remove",
                new { Reason = "A report about this listing was upheld." }))
            .ReadAsync<AdminPropertyDto>();

        removed.HasInspectionBadge.ShouldBeFalse();
    }

    [Fact]
    public async Task A_badge_that_is_not_there_cannot_be_removed()
    {
        var (_, admin, listing) = await PublishedAsync("badge-remove-none");

        (await admin.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/badge/remove", new { Reason = "None." }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_badge_change_with_no_reason_is_refused(string reason)
    {
        var (_, admin, listing) = await PublishedAsync($"badge-reason-{reason.Length}");

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var response = await GrantAsync(admin, listing.Id, reason);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    /// <summary>
    /// Rule 113. Read the listing first so it is cached, then take the badge off, then
    /// read it again — the direction that matters, because a stale badge is a claim
    /// RoomRaah has stopped standing behind.
    /// </summary>
    [Fact]
    public async Task Removing_a_badge_is_visible_on_the_public_listing_immediately()
    {
        var (owner, admin, listing) = await PublishedAsync("badge-cache");

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var badged = await (await owner.Client.GetAsync($"/api/v1/properties/{listing.Id}"))
            .ReadAsync<ListingDetailDto>();
        badged.HasInspectionBadge.ShouldBeTrue();

        (await admin.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/badge/remove",
                new { Reason = "Re-inspection failed." }))
            .EnsureSuccessStatusCode();

        var after = await (await owner.Client.GetAsync($"/api/v1/properties/{listing.Id}"))
            .ReadAsync<ListingDetailDto>();

        after.HasInspectionBadge.ShouldBeFalse();
    }

    // ------------------------------------------------------ worklist (rule 114)

    [Fact]
    public async Task A_listing_visited_recently_is_not_on_the_worklist()
    {
        var (_, admin, listing) = await PublishedAsync("due-fresh");

        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var due = await (await admin.Client.GetAsync(
                "/api/v1/admin/inspections/due?pageSize=50"))
            .ReadAsync<PagedResult<DueForInspectionDto>>();

        due.Items.Select(d => d.PropertyId).ShouldNotContain(listing.Id);
    }

    [Fact]
    public async Task A_badged_listing_nobody_has_visited_in_a_month_is_on_the_worklist()
    {
        var (_, admin, listing) = await PublishedAsync("due-stale");

        (await RecordAsync(admin, listing.Id, at: DateTime.UtcNow.AddDays(-45)))
            .EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var due = await (await admin.Client.GetAsync(
                "/api/v1/admin/inspections/due?pageSize=50"))
            .ReadAsync<PagedResult<DueForInspectionDto>>();

        var row = due.Items.Single(d => d.PropertyId == listing.Id);

        row.DaysOverdue.ShouldBe(15);
        row.OwnerDisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A listing with no badge is not on RoomRaah's schedule at all.</summary>
    [Fact]
    public async Task An_unbadged_listing_is_never_on_the_worklist()
    {
        var (_, admin, listing) = await PublishedAsync("due-unbadged");

        (await RecordAsync(admin, listing.Id, at: DateTime.UtcNow.AddDays(-90)))
            .EnsureSuccessStatusCode();

        var due = await (await admin.Client.GetAsync(
                "/api/v1/admin/inspections/due?pageSize=50"))
            .ReadAsync<PagedResult<DueForInspectionDto>>();

        due.Items.Select(d => d.PropertyId).ShouldNotContain(listing.Id);
    }

    // -------------------------------------------------------------- privacy

    /// <summary>
    /// Rule 115, on the raw body. What the owner is told is what has happened; there is
    /// no date in the product for what is going to.
    /// </summary>
    [Fact]
    public async Task The_owner_is_never_told_when_the_next_visit_is()
    {
        var (owner, admin, listing) = await PublishedAsync("inspect-privacy");

        (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .EnsureSuccessStatusCode();
        (await RecordAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var state = await (await owner.Client.PostJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/inspection-request", new { }))
            .Content.ReadAsStringAsync();

        var detail = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .Content.ReadAsStringAsync();

        foreach (var body in new[] { state, detail })
        {
            foreach (var word in new[] { "scheduled", "nextInspection", "nextVisit", "dueAt" })
            {
                body.ShouldNotContain(word, Case.Insensitive);
            }
        }
    }

    /// <summary>Rule 33: the worklist names an owner and gives no way to reach them.</summary>
    [Fact]
    public async Task The_worklist_carries_no_owner_contact_details()
    {
        var (owner, admin, listing) = await PublishedAsync("due-privacy");

        (await RecordAsync(admin, listing.Id, at: DateTime.UtcNow.AddDays(-45)))
            .EnsureSuccessStatusCode();
        (await GrantAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        var body = await (await admin.Client.GetAsync("/api/v1/admin/inspections/due?pageSize=50"))
            .Content.ReadAsStringAsync();

        body.ShouldNotContain(owner.Email);
        body.ShouldNotContain("+92300");
    }

    [Fact]
    public async Task A_seeker_cannot_reach_any_of_this()
    {
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "inspect-seeker");

        (await seeker.Client.GetAsync("/api/v1/admin/inspections/due"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await seeker.Client.PostJsonAsync("/api/v1/admin/inspections", new { PropertyId = 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
