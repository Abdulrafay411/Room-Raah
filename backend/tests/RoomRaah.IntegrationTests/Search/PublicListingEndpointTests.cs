using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Search;

/// <summary>
/// The public detail page, its gallery, the landing page and a comparison — and the
/// two things this phase is really measured by: that nothing unapproved reaches a
/// stranger, and that nothing a stranger reads names a way to contact an owner.
/// </summary>
[Collection(ApiCollection.Name)]
public class PublicListingEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public PublicListingEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<(OwnerWorkspace Owner, AdminWorkspace Admin, OwnerPropertyDto Listing)> Published(
        string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);

        return (owner, admin, await owner.CreatePublishedAsync(admin));
    }

    private async Task<ListingDetailDto> DetailAsync(int id)
    {
        var response = await Anonymous().GetAsync($"/api/v1/properties/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadAsync<ListingDetailDto>();
    }

    private async Task<List<ListingPhotoDto>> GalleryAsync(int id)
    {
        return await (await Anonymous().GetAsync($"/api/v1/properties/{id}/photos"))
            .ReadAsync<List<ListingPhotoDto>>();
    }

    private async Task ApprovePendingRevisionAsync(AdminWorkspace admin, int propertyId)
    {
        var queue = await (await admin.Client.GetAsync("/api/v1/admin/revisions?pageSize=50"))
            .ReadAsync<PagedResult<RevisionQueueRowDto>>();

        var row = queue.Items.FirstOrDefault(r => r.PropertyId == propertyId)
            ?? throw new InvalidOperationException($"No revision is pending on {propertyId}.");

        (await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{row.Id}/approve", new { }))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_stranger_with_no_token_can_read_a_published_listing()
    {
        var (_, _, listing) = await Published("detail");

        var detail = await DetailAsync(listing.Id);

        detail.Id.ShouldBe(listing.Id);
        detail.Description.ShouldNotBeNullOrWhiteSpace();
        detail.AddressLine.ShouldNotBeNullOrWhiteSpace();
        detail.Photos.Count.ShouldBe(3);
        detail.Owner.DisplayName.ShouldNotBeNullOrWhiteSpace();
        detail.Owner.IdentityVerified.ShouldBeTrue();
        detail.Checks.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Rule_28_a_draft_is_not_found_on_the_public_route()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "public-draft");
        var draft = await owner.CreateDraftAsync();

        (await Anonymous().GetAsync($"/api/v1/properties/{draft.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A public endpoint that quietly widened for a signed-in reader is a public
    /// endpoint nobody can reason about. The owner has their own route to this.
    /// </summary>
    [Fact]
    public async Task Rule_28_a_draft_is_not_found_even_for_the_owner_who_wrote_it()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "own-draft");
        var draft = await owner.CreateDraftAsync();

        (await owner.Client.GetAsync($"/api/v1/properties/{draft.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await owner.Client.GetAsync($"/api/v1/owner/properties/{draft.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_listing_that_never_existed_is_not_found()
    {
        (await Anonymous().GetAsync("/api/v1/properties/999999"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await Anonymous().GetAsync("/api/v1/properties/999999/photos"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Rules 33 and 84, asserted against the raw body rather than the DTO, so that a
    /// field added to a shape later cannot slip an address or a phone number onto a
    /// public page without this failing.
    /// </summary>
    [Fact]
    public async Task Rules_33_and_84_no_public_response_names_a_way_to_reach_the_owner()
    {
        var (owner, _, listing) = await Published("privacy");

        string[] urls =
        [
            $"/api/v1/properties/{listing.Id}",
            $"/api/v1/properties/{listing.Id}/photos",
            "/api/v1/properties?pageSize=50",
            "/api/v1/properties/featured",
            $"/api/v1/properties/compare?ids={listing.Id}"
        ];

        foreach (var url in urls)
        {
            var body = await (await Anonymous().GetAsync(url)).Content.ReadAsStringAsync();

            body.ShouldNotContain(owner.Email);
            body.ShouldNotContain("+92300111");
            body.ShouldNotContain("3520112345671");
            body.ToLowerInvariant().ShouldNotContain("whatsapp");
        }
    }

    [Fact]
    public async Task Rule_84_a_public_check_carries_no_admin_note()
    {
        var (_, admin, listing) = await Published("checknote");

        (await admin.Client.PostJsonAsync($"/api/v1/admin/properties/{listing.Id}/verify",
            new { CheckType = "Address", Status = "Checked", Note = "Matched the pin on the map." }))
            .EnsureSuccessStatusCode();

        var body = await (await Anonymous().GetAsync($"/api/v1/properties/{listing.Id}"))
            .Content.ReadAsStringAsync();

        body.ShouldNotContain("Matched the pin on the map.");
    }

    [Fact]
    public async Task The_gallery_endpoint_and_the_detail_page_agree()
    {
        var (_, _, listing) = await Published("gallery");

        var gallery = await GalleryAsync(listing.Id);
        var detail = await DetailAsync(listing.Id);

        gallery.Select(p => p.Id).ShouldBe(detail.Photos.Select(p => p.Id));
        gallery.Count(p => p.IsPrimary).ShouldBe(1);
        gallery.Select(p => p.SortOrder).ShouldBe([1, 2, 3]);
    }

    /// <summary>
    /// Rule 79. A photograph nobody has reviewed must not appear, and approving the
    /// revision has to be what makes it appear — which also proves rule 71 dropped
    /// the cached gallery.
    /// </summary>
    [Fact]
    public async Task Rule_79_a_photograph_waiting_for_an_admin_is_invisible_until_it_is_approved()
    {
        var (owner, admin, listing) = await Published("pendingadd");

        var added = await owner.UploadAsync(listing.Id);
        added.Status.ShouldBe("PendingAdd");

        var beforeGallery = await GalleryAsync(listing.Id);
        beforeGallery.Select(p => p.Id).ShouldNotContain(added.Id);
        beforeGallery.Count.ShouldBe(3);

        (await DetailAsync(listing.Id)).PhotoCount.ShouldBe(3);

        await ApprovePendingRevisionAsync(admin, listing.Id);

        var afterGallery = await GalleryAsync(listing.Id);
        afterGallery.Select(p => p.Id).ShouldContain(added.Id);
        afterGallery.Count.ShouldBe(4);
    }

    /// <summary>
    /// Rule 79 from the other side. The owner has asked to remove it; until an admin
    /// agrees, the listing still shows what it showed yesterday.
    /// </summary>
    [Fact]
    public async Task Rule_79_a_photograph_waiting_to_be_removed_is_still_public()
    {
        var (owner, admin, listing) = await Published("pendingremove");

        var doomed = listing.Photos.Last();

        (await owner.Client.DeleteAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/{doomed.Id}"))
            .EnsureSuccessStatusCode();

        (await GalleryAsync(listing.Id)).Select(p => p.Id).ShouldContain(doomed.Id);

        await ApprovePendingRevisionAsync(admin, listing.Id);

        (await GalleryAsync(listing.Id)).Select(p => p.Id).ShouldNotContain(doomed.Id);
    }

    /// <summary>
    /// The heart of rule 19 seen from the public side, and rules 32 and 71 with it:
    /// the live page does not move while a revision waits, and the moment an admin
    /// approves it the new rent is what a stranger reads — not what the cache holds.
    /// </summary>
    [Fact]
    public async Task Rules_19_and_32_the_public_page_holds_still_until_the_revision_is_approved()
    {
        var (owner, admin, listing) = await Published("revision");

        (await DetailAsync(listing.Id)).MonthlyRent.ShouldBe(18000m);

        (await owner.Client.PutJsonAsync($"/api/v1/owner/properties/{listing.Id}",
                OwnerWorkspace.EditOf(listing, fields => fields["monthlyRent"] = 21000)))
            .EnsureSuccessStatusCode();

        (await DetailAsync(listing.Id)).MonthlyRent.ShouldBe(18000m);

        await ApprovePendingRevisionAsync(admin, listing.Id);

        (await DetailAsync(listing.Id)).MonthlyRent.ShouldBe(21000m);
    }

    /// <summary>
    /// Rule 32 for the other of the two fields, through the availability route: the
    /// bed count is the number the whole product is judged on being right about.
    /// </summary>
    [Fact]
    public async Task Rule_32_an_approved_bed_count_is_visible_at_once()
    {
        var (owner, admin, listing) = await Published("beds");

        (await DetailAsync(listing.Id)).AvailableBeds.ShouldBe(2);

        (await owner.Client.PatchJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/availability", new { AvailableBeds = 0 }))
            .EnsureSuccessStatusCode();

        (await DetailAsync(listing.Id)).AvailableBeds.ShouldBe(2);

        await ApprovePendingRevisionAsync(admin, listing.Id);

        (await DetailAsync(listing.Id)).AvailableBeds.ShouldBe(0);
    }

    /// <summary>
    /// Rule 27, read from the public side. A rejection changes nothing about the
    /// listing, and rule 71 drops the cache anyway — which is what makes this a
    /// statement about the database rather than about what Redis happened to hold.
    /// </summary>
    [Fact]
    public async Task Rule_27_a_rejected_revision_leaves_the_public_page_exactly_as_it_was()
    {
        var (owner, admin, listing) = await Published("unpublish");

        await DetailAsync(listing.Id);

        (await owner.Client.PutJsonAsync($"/api/v1/owner/properties/{listing.Id}",
                OwnerWorkspace.EditOf(listing, fields => fields["title"] = "A different title")))
            .EnsureSuccessStatusCode();

        var queue = await (await admin.Client.GetAsync("/api/v1/admin/revisions?pageSize=50"))
            .ReadAsync<PagedResult<RevisionQueueRowDto>>();

        var row = queue.Items.First(r => r.PropertyId == listing.Id);

        (await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{row.Id}/reject",
                new { AdminNote = "The title does not describe the room." }))
            .EnsureSuccessStatusCode();

        (await DetailAsync(listing.Id)).Title.ShouldBe(listing.Title);
    }

    /// <summary>
    /// The ordering assertion on its own is not enough: once enough unbadged listings
    /// exist, a featured list that sorted the badge the wrong way round would be eight
    /// unbadged rows, and eight identical values are in order whichever way they are
    /// read. So the inspected listings the seeder writes have to be in there too —
    /// they are older than everything the tests create, and the badge is the only
    /// reason they would make the top eight.
    /// </summary>
    [Fact]
    public async Task Rule_82_the_landing_page_puts_inspected_listings_first()
    {
        var client = Anonymous();

        var featured = await (await client.GetAsync("/api/v1/properties/featured"))
            .ReadAsync<List<ListingCardDto>>();

        featured.ShouldNotBeEmpty();
        featured.Count.ShouldBeLessThanOrEqualTo(8);

        var badges = featured.Select(c => c.HasInspectionBadge).ToList();
        badges.ShouldBe(badges.OrderDescending());

        var published = await (await client.GetAsync("/api/v1/properties?pageSize=50"))
            .ReadAsync<PagedResult<ListingCardDto>>();

        var inspected = published.Items.Where(c => c.HasInspectionBadge).Select(c => c.Id).ToList();

        inspected.Count.ShouldBeInRange(1, 8);

        foreach (var id in inspected)
        {
            featured.ShouldContain(card => card.Id == id,
                $"listing {id} carries the inspection badge and is not on the landing page");
        }
    }

    [Fact]
    public async Task Rule_82_the_landing_page_shows_only_published_listings()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "featured-draft");
        var draft = await owner.CreateDraftAsync();

        var featured = await (await Anonymous().GetAsync("/api/v1/properties/featured"))
            .ReadAsync<List<ListingCardDto>>();

        featured.ShouldNotContain(card => card.Id == draft.Id);
    }

    [Fact]
    public async Task Rule_81_a_comparison_comes_back_in_the_order_it_was_asked_for()
    {
        var (_, _, first) = await Published("compare-a");
        var (_, _, second) = await Published("compare-b");

        var listings = await (await Anonymous()
                .GetAsync($"/api/v1/properties/compare?ids={second.Id},{first.Id}"))
            .ReadAsync<List<ListingComparisonDto>>();

        listings.Select(l => l.Id).ShouldBe([second.Id, first.Id]);
        listings[0].Facilities.ShouldNotBeNull();
        listings[0].AddressLine.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rule_81_an_id_that_is_not_published_is_left_out()
    {
        var (owner, _, published) = await Published("compare-drop");
        var draft = await owner.CreateDraftAsync();

        var listings = await (await Anonymous()
                .GetAsync($"/api/v1/properties/compare?ids={published.Id},{draft.Id}"))
            .ReadAsync<List<ListingComparisonDto>>();

        listings.Select(l => l.Id).ShouldBe([published.Id]);
    }

    [Fact]
    public async Task Rule_35_a_fourth_listing_is_refused()
    {
        (await Anonymous().GetAsync("/api/v1/properties/compare?ids=1,2,3,4"))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_81_the_same_id_three_times_is_one_listing_and_not_a_refusal()
    {
        var (_, _, listing) = await Published("compare-dupes");

        var response = await Anonymous()
            .GetAsync($"/api/v1/properties/compare?ids={listing.Id},{listing.Id},{listing.Id},{listing.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<List<ListingComparisonDto>>()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_comparison_with_no_ids_is_refused()
    {
        (await Anonymous().GetAsync("/api/v1/properties/compare"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_comparison_can_be_measured_from_a_landmark()
    {
        var (_, _, listing) = await Published("compare-distance");

        var suggestions = await (await Anonymous().GetAsync("/api/v1/locations/suggest?q=Punjab"))
            .ReadAsync<List<LocationSuggestionDto>>();

        var landmarkId = suggestions.First(s => s.Type == LocationSuggestionDto.LandmarkType).Id;

        var listings = await (await Anonymous()
                .GetAsync($"/api/v1/properties/compare?ids={listing.Id}&landmarkId={landmarkId}"))
            .ReadAsync<List<ListingComparisonDto>>();

        listings.Single().DistanceKm.ShouldBe(
            OwnerWorkspace.FixtureDistanceFromPunjabUniversityKm);
    }
}
