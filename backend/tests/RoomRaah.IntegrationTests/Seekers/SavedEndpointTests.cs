using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Seekers;

[Collection(ApiCollection.Name)]
public class SavedEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public SavedEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(SeekerWorkspace Seeker, OwnerWorkspace Owner, OwnerPropertyDto Listing)>
        PublishedAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, label);

        return (seeker, owner, listing);
    }

    [Fact]
    public async Task A_stranger_has_no_shortlist()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/saved");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_published_listing_goes_on_and_comes_off_again()
    {
        var (seeker, _, listing) = await PublishedAsync("saved-round-trip");

        var save = await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { });

        save.StatusCode.ShouldBe(HttpStatusCode.Created);

        var card = await save.ReadAsync<SavedListingDto>();
        card.Id.ShouldBe(listing.Id);
        card.SavedAt.ShouldNotBe(default);

        var page = await (await seeker.Client.GetAsync("/api/v1/saved"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        page.Items.Select(i => i.Id).ShouldContain(listing.Id);

        var remove = await seeker.Client.DeleteAsync($"/api/v1/saved/{listing.Id}");
        remove.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await (await seeker.Client.GetAsync("/api/v1/saved"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        after.Items.Select(i => i.Id).ShouldNotContain(listing.Id);
    }

    [Fact]
    public async Task Rule_34_the_same_listing_cannot_be_saved_twice()
    {
        var (seeker, _, listing) = await PublishedAsync("saved-twice");

        (await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        var again = await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_86_a_draft_cannot_be_saved()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "saved-draft");
        var draft = await owner.CreateDraftAsync();
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "saved-draft");

        var response = await seeker.Client.PostJsonAsync($"/api/v1/saved/{draft.Id}", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Removing_a_listing_that_was_never_saved_is_not_found()
    {
        var (seeker, _, listing) = await PublishedAsync("saved-missing");

        var response = await seeker.Client.DeleteAsync($"/api/v1/saved/{listing.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Rule 87, both halves at once. A card for a listing an admin has taken down is
    /// a card nobody should be reading, so the shortlist stops showing it; the row
    /// survives, so the person who built the shortlist can still clear it.
    /// </summary>
    [Fact]
    public async Task Rule_87_a_listing_that_leaves_the_site_leaves_the_shortlist_and_stays_deletable()
    {
        var (seeker, _, listing) = await PublishedAsync("saved-unpublished");

        (await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        var before = await (await seeker.Client.GetAsync("/api/v1/saved?pageSize=50"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        before.Items.Select(i => i.Id).ShouldContain(listing.Id);

        await SeekerWorkspace.UnpublishAsync(_factory, listing.Id);

        var after = await (await seeker.Client.GetAsync("/api/v1/saved?pageSize=50"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        after.Items.Select(i => i.Id).ShouldNotContain(listing.Id);
        after.TotalCount.ShouldBe(before.TotalCount - 1);

        // And it can still be cleared, which is the half a status filter on the read
        // path would quietly break.
        var removed = await seeker.Client.DeleteAsync($"/api/v1/saved/{listing.Id}");

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task One_persons_shortlist_is_not_another_persons()
    {
        var (first, _, listing) = await PublishedAsync("saved-private");
        var second = await SeekerWorkspace.CreateAsync(_factory, "saved-private-other");

        (await first.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        var theirs = await (await second.Client.GetAsync("/api/v1/saved"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        theirs.Items.Select(i => i.Id).ShouldNotContain(listing.Id);
    }

    /// <summary>
    /// Rule 33 on a page only the person who built it can read. A shortlist is made
    /// of cards, and a card is where an owner's details would show up first.
    /// </summary>
    [Fact]
    public async Task Rule_33_a_shortlist_names_no_way_to_reach_an_owner()
    {
        var (seeker, owner, listing) = await PublishedAsync("saved-privacy");

        (await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        var body = await (await seeker.Client.GetAsync("/api/v1/saved")).Content.ReadAsStringAsync();

        body.ShouldNotContain(owner.Email);
        body.ShouldNotContain("+9230011122");
        body.ShouldNotContain("phoneNumber");
        body.ShouldNotContain("cnic", Case.Insensitive);
    }
}
