using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.AdminListings;

/// <summary>
/// The admin side of a listing, against a real database, a real cache and real blob
/// storage. The journey these tests walk is the one the phase is measured by: a
/// listing is approved and becomes public, its owner edits it, the public keeps
/// seeing the approved version, and approving the revision swaps it in.
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminReviewEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public AdminReviewEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(OwnerWorkspace Owner, AdminWorkspace Admin, OwnerPropertyDto Listing)> Submitted(
        string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreateSubmittableAsync();

        (await owner.Client.PostJsonAsync($"/api/v1/owner/properties/{listing.Id}/submit", new { }))
            .EnsureSuccessStatusCode();

        return (owner, admin, listing);
    }

    [Fact]
    public async Task Rule_23_a_listing_cannot_be_published_until_every_check_has_passed()
    {
        var (_, admin, listing) = await Submitted("gate");

        var tooEarly = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/approve", new { });

        tooEarly.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var before = await (await admin.Client.GetAsync($"/api/v1/admin/properties/{listing.Id}"))
            .ReadAsync<AdminPropertyDto>();

        before.CanPublish.ShouldBeFalse();

        await admin.PassEveryCheckAsync(listing.Id);

        var after = await (await admin.Client.GetAsync($"/api/v1/admin/properties/{listing.Id}"))
            .ReadAsync<AdminPropertyDto>();

        after.CanPublish.ShouldBeTrue();

        var approved = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/approve", new { });

        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await approved.ReadAsync<AdminPropertyDto>()).Status.ShouldBe("Published");
    }

    [Fact]
    public async Task Rule_64_recording_the_same_check_again_replaces_the_verdict()
    {
        var (_, admin, listing) = await Submitted("recheck");

        await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/verify",
            new { CheckType = "Address", Status = "Failed", Note = "Bill is for a different house." });

        var second = await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/verify",
            new { CheckType = "Address", Status = "Checked", Note = "Owner sent the right bill." });

        var checks = await second.ReadAsync<List<VerificationCheckDto>>();

        checks.Count(c => c.CheckType == "Address").ShouldBe(1);
        checks.Single(c => c.CheckType == "Address").Status.ShouldBe("Checked");
        checks.Single(c => c.CheckType == "Address").Note.ShouldBe("Owner sent the right bill.");
    }

    [Fact]
    public async Task Rule_26_a_rejection_without_a_note_is_refused_and_one_with_a_note_is_not()
    {
        var (_, admin, listing) = await Submitted("reject");

        var silent = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/reject", new { });

        silent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await silent.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContain(e => e.Field == "AdminNote");

        var explained = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/reject",
            new { AdminNote = "The address does not match the utility bill." });

        (await explained.ReadAsync<AdminPropertyDto>()).Status.ShouldBe("Rejected");
    }

    [Fact]
    public async Task Rule_63_a_request_for_changes_sends_the_listing_back_with_the_note()
    {
        var (owner, admin, listing) = await Submitted("changes");

        (await admin.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/request-changes", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var sent = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/request-changes",
            new { AdminNote = "Please add a photo of the kitchen." });

        (await sent.ReadAsync<AdminPropertyDto>()).Status.ShouldBe("ChangesRequested");

        // The owner sees it too, and rule 52 lets them submit it again.
        var mine = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        mine.Status.ShouldBe("ChangesRequested");
    }

    [Fact]
    public async Task Rule_70_every_decision_emails_the_owner_with_the_note()
    {
        var (owner, admin, listing) = await Submitted("email");

        await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{listing.Id}/reject",
            new { AdminNote = "The address does not match the utility bill." });

        var sent = _factory.Emails.DecisionsFor(owner.Email);

        sent.ShouldNotBeEmpty();
        sent[^1].Decision.ShouldBe("Rejected");
        sent[^1].Note.ShouldBe("The address does not match the utility bill.");
    }

    [Fact]
    public async Task Rule_62_a_listing_that_is_not_waiting_for_review_cannot_be_decided()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "draft");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var draft = await owner.CreateDraftAsync();

        var response = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{draft.Id}/approve", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task The_queue_shows_a_submitted_listing_and_the_detail_shows_its_checks()
    {
        var (_, admin, listing) = await Submitted("queue");

        var page = await (await admin.Client.GetAsync(
                "/api/v1/admin/properties?status=PendingReview&pageSize=50"))
            .ReadAsync<PagedResult<AdminPropertySummaryDto>>();

        page.Items.ShouldContain(p => p.Id == listing.Id);
        page.Items.First(p => p.Id == listing.Id).OwnerName.ShouldNotBeNullOrWhiteSpace();

        await admin.PassEveryCheckAsync(listing.Id);

        var detail = await (await admin.Client.GetAsync($"/api/v1/admin/properties/{listing.Id}"))
            .ReadAsync<AdminPropertyDto>();

        detail.VerificationChecks.Count.ShouldBe(4);
        detail.Photos.Count.ShouldBe(3);
        detail.Owner.IdentityStatus.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Rule 33 has no exception for an admin panel. Asserted against the raw body so
    /// that a field added to a DTO later cannot slip an address through unnoticed.
    /// </summary>
    [Fact]
    public async Task Rule_33_no_admin_response_carries_a_way_to_contact_the_owner()
    {
        var (owner, admin, listing) = await Submitted("privacy");

        foreach (var url in new[]
                 {
                     "/api/v1/admin/properties?pageSize=50",
                     $"/api/v1/admin/properties/{listing.Id}"
                 })
        {
            var body = await (await admin.Client.GetAsync(url)).Content.ReadAsStringAsync();

            body.ShouldNotContain(owner.Email);
            body.ShouldNotContain("+92300111");
            body.ShouldNotContain("3520112345671");
        }
    }

    [Fact]
    public async Task Rule_22_an_owner_cannot_reach_the_review_endpoints()
    {
        var (owner, _, listing) = await Submitted("role");

        (await owner.Client.GetAsync("/api/v1/admin/properties"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await owner.Client.GetAsync($"/api/v1/admin/properties/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await owner.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/approve", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await owner.Client.GetAsync("/api/v1/admin/revisions"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_is_turned_away_before_anything_else()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/admin/properties"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_a_404()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        (await admin.Client.GetAsync("/api/v1/admin/properties/987654"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await admin.Client.GetAsync("/api/v1/admin/revisions/987654"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/v1/admin/properties?status=Sold")]
    [InlineData("/api/v1/admin/properties?pageSize=51")]
    [InlineData("/api/v1/admin/revisions?kind=Everything")]
    public async Task A_query_the_rules_do_not_recognise_is_a_400(string url)
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        (await admin.Client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
