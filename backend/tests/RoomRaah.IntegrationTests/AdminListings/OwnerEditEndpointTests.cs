using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.AdminListings;

/// <summary>
/// What an owner's edit does, on both sides of publication. This is the phase's own
/// measure of done: the owner edits a published listing, the live listing keeps
/// serving the approved version, and approving the revision swaps it in.
/// </summary>
[Collection(ApiCollection.Name)]
public class OwnerEditEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public OwnerEditEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(OwnerWorkspace Owner, AdminWorkspace Admin, OwnerPropertyDto Listing)> Published(
        string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);

        return (owner, admin, await owner.CreatePublishedAsync(admin));
    }

    private static Task<HttpResponseMessage> EditAsync(
        OwnerWorkspace owner, OwnerPropertyDto listing, Action<Dictionary<string, object?>> change)
    {
        return owner.Client.PutJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}",
            OwnerWorkspace.EditOf(listing, change));
    }

    [Fact]
    public async Task Rule_60_a_draft_is_edited_in_place()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "draftedit");
        var draft = await owner.CreateDraftAsync();

        var response = await EditAsync(owner, draft, f => f["title"] = "A quieter room, same street");

        var result = await response.ReadAsync<OwnerListingUpdateDto>();

        result.PendingRevision.ShouldBeNull();
        result.Listing.Title.ShouldBe("A quieter room, same street");
    }

    /// <summary>
    /// The whole point of the phase. A good listing must not vanish from the site
    /// because its owner fixed a typo.
    /// </summary>
    [Fact]
    public async Task Rule_19_editing_a_published_listing_leaves_it_published_and_unchanged()
    {
        var (owner, _, listing) = await Published("revise");

        var response = await EditAsync(owner, listing, f =>
        {
            f["title"] = "Renamed while it is live";
            f["monthlyRent"] = 21000;
        });

        var result = await response.ReadAsync<OwnerListingUpdateDto>();

        result.Listing.Status.ShouldBe("Published");
        result.Listing.Title.ShouldBe(listing.Title);
        result.Listing.MonthlyRent.ShouldBe(listing.MonthlyRent);

        var revision = result.PendingRevision.ShouldNotBeNull();

        revision.Kind.ShouldBe("Content");
        revision.Changes.Select(c => c.Field).ShouldBe(["Title", "MonthlyRent"], ignoreOrder: true);
        revision.Changes.Single(c => c.Field == "MonthlyRent").Current.ShouldBe("18000");
        revision.Changes.Single(c => c.Field == "MonthlyRent").Proposed.ShouldBe("21000");
    }

    [Fact]
    public async Task Rule_25_approving_the_revision_swaps_the_new_version_in()
    {
        var (owner, admin, listing) = await Published("approve");

        var proposed = await (await EditAsync(owner, listing, f =>
        {
            f["title"] = "Renamed and approved";
            f["monthlyRent"] = 21000;
        })).ReadAsync<OwnerListingUpdateDto>();

        var revisionId = proposed.PendingRevision!.Id;

        var decided = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/revisions/{revisionId}/approve", new { });

        (await decided.ReadAsync<RevisionDetailDto>()).ReviewStatus.ShouldBe("Approved");

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Title.ShouldBe("Renamed and approved");
        after.MonthlyRent.ShouldBe(21000m);
        after.Status.ShouldBe("Published");
    }

    [Fact]
    public async Task Rule_27_rejecting_the_revision_leaves_the_listing_exactly_as_it_was()
    {
        var (owner, admin, listing) = await Published("rejectrev");

        var proposed = await (await EditAsync(owner, listing, f => f["monthlyRent"] = 99000))
            .ReadAsync<OwnerListingUpdateDto>();

        var revisionId = proposed.PendingRevision!.Id;

        var silent = await admin.Client.PostJsonAsync(
            $"/api/v1/admin/revisions/{revisionId}/reject", new { });

        silent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revisionId}/reject",
            new { AdminNote = "That is above the range for the area." });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.MonthlyRent.ShouldBe(listing.MonthlyRent);

        // And a decided revision cannot be decided again.
        (await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revisionId}/approve", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_21_a_bed_count_on_its_own_lands_in_the_availability_queue()
    {
        var (owner, admin, listing) = await Published("beds");

        var confirmed = await owner.Client.PatchJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}/availability", new { AvailableBeds = 1 });

        var revision = (await confirmed.ReadAsync<OwnerListingUpdateDto>()).PendingRevision
            .ShouldNotBeNull();

        revision.Kind.ShouldBe("Availability");

        var queue = await (await admin.Client.GetAsync(
                "/api/v1/admin/revisions?kind=Availability&pageSize=50"))
            .ReadAsync<PagedResult<RevisionQueueRowDto>>();

        queue.Items.ShouldContain(r => r.Id == revision.Id);

        var row = queue.Items.First(r => r.Id == revision.Id);

        row.CurrentAvailableBeds.ShouldBe(2);
        row.ProposedAvailableBeds.ShouldBe(1);
        row.TotalBeds.ShouldBe(4);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/approve", new { });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.AvailableBeds.ShouldBe(1);
        after.AvailabilityConfirmedAt.ShouldBeGreaterThan(listing.AvailabilityConfirmedAt);
    }

    /// <summary>
    /// Rule 73. Both kinds can be open at once, and each was written against the live
    /// row rather than against the other, so approving the content revision must not
    /// carry back the bed count it happened to capture.
    /// </summary>
    [Fact]
    public async Task Rule_73_approving_a_content_revision_does_not_undo_an_availability_one()
    {
        var (owner, admin, listing) = await Published("bothkinds");

        var content = (await (await EditAsync(owner, listing, f => f["title"] = "Renamed"))
            .ReadAsync<OwnerListingUpdateDto>()).PendingRevision!;

        var availability = (await (await owner.Client.PatchJsonAsync(
                    $"/api/v1/owner/properties/{listing.Id}/availability", new { AvailableBeds = 0 }))
                .ReadAsync<OwnerListingUpdateDto>()).PendingRevision!;

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{availability.Id}/approve", new { });
        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{content.Id}/approve", new { });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Title.ShouldBe("Renamed");
        after.AvailableBeds.ShouldBe(0);
    }

    [Fact]
    public async Task Rule_61_an_edit_that_changes_nothing_is_refused()
    {
        var (owner, _, listing) = await Published("nochange");

        var response = await owner.Client.PutJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}", OwnerWorkspace.EditOf(listing));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_20_a_second_revision_of_the_same_kind_is_refused()
    {
        var (owner, _, listing) = await Published("twice");

        (await EditAsync(owner, listing, f => f["title"] = "First"))
            .EnsureSuccessStatusCode();

        (await EditAsync(owner, listing, f => f["title"] = "Second"))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_60_a_listing_waiting_for_review_refuses_the_edit()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "underreview");
        var listing = await owner.CreateSubmittableAsync();

        (await owner.Client.PostJsonAsync($"/api/v1/owner/properties/{listing.Id}/submit", new { }))
            .EnsureSuccessStatusCode();

        (await EditAsync(owner, listing, f => f["title"] = "Changed mid-review"))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_18_another_owner_cannot_edit_this_listing()
    {
        var (_, _, listing) = await Published("stranger");
        var stranger = await OwnerWorkspace.CreateAsync(_factory, "stranger2");

        var response = await stranger.Client.PutJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}", OwnerWorkspace.EditOf(listing));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task More_free_beds_than_the_room_has_is_a_field_error()
    {
        var (owner, _, listing) = await Published("toomany");

        var response = await owner.Client.PatchJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}/availability", new { AvailableBeds = 99 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContain(e => e.Field == "AvailableBeds");
    }
}
