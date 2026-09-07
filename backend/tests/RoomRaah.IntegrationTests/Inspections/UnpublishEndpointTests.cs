using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Inspections;

/// <summary>
/// Rules 122 to 124 — the gap Phase 6 recorded and deliberately did not close.
///
/// §5 had ten admin listing endpoints and none of them set <c>Unpublished</c>, so the
/// status was in the enumeration and unreachable in the product: a listing could never
/// leave the public site through the API at all. The seeker tests had to write the
/// status directly to test rule 87 because of it.
/// </summary>
[Collection(ApiCollection.Name)]
public class UnpublishEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public UnpublishEndpointTests(RoomRaahApiFactory factory)
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

    private static Task<HttpResponseMessage> UnpublishAsync(
        AdminWorkspace admin, int propertyId, string? note = "The listing no longer matches the property.")
    {
        return admin.Client.PostJsonAsync(
            $"/api/v1/admin/properties/{propertyId}/unpublish", new { AdminNote = note });
    }

    [Fact]
    public async Task A_listing_leaves_the_public_site_and_can_be_put_back()
    {
        var (_, admin, listing) = await PublishedAsync("unpublish-journey");

        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}")).EnsureSuccessStatusCode();

        var taken = await (await UnpublishAsync(admin, listing.Id)).ReadAsync<AdminPropertyDto>();

        taken.Status.ShouldBe("Unpublished");

        // Rule 123, and the reason it exists: a listing already sitting in a cached
        // detail response does not leave it because a status changed.
        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Rule 124: back through the endpoint that was always there.
        (await admin.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/approve", new { }))
            .EnsureSuccessStatusCode();

        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_unpublished_listing_is_gone_from_search_as_well()
    {
        var (_, admin, listing) = await PublishedAsync("unpublish-search");

        var anonymous = _factory.CreateClient();

        await UnpublishAsync(admin, listing.Id);

        var page = await (await anonymous.GetAsync("/api/v1/properties?pageSize=50"))
            .ReadAsync<PagedResult<ListingCardDto>>();

        page.Items.Select(c => c.Id).ShouldNotContain(listing.Id);
    }

    /// <summary>
    /// Rule 123 and rule 87 together. The card goes; the saved row stays, and the
    /// shortlist can still be cleared — which is exactly the case the seeker tests had
    /// to reach by writing the status by hand, because no endpoint could produce it.
    /// </summary>
    [Fact]
    public async Task A_shortlist_loses_the_card_and_keeps_the_row()
    {
        var (_, admin, listing) = await PublishedAsync("unpublish-shortlist");
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "unpublish-shortlist");

        (await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        await UnpublishAsync(admin, listing.Id);

        var shortlist = await (await seeker.Client.GetAsync("/api/v1/saved"))
            .ReadAsync<PagedResult<SavedListingDto>>();

        shortlist.Items.Select(s => s.Id).ShouldNotContain(listing.Id);

        (await seeker.Client.DeleteAsync($"/api/v1/saved/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>Rule 122: only from `Published`.</summary>
    [Fact]
    public async Task A_draft_cannot_be_unpublished()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "unpublish-draft");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var draft = await owner.CreateDraftAsync();

        (await UnpublishAsync(admin, draft.Id)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_listing_cannot_be_unpublished_twice()
    {
        var (_, admin, listing) = await PublishedAsync("unpublish-twice");

        (await UnpublishAsync(admin, listing.Id)).EnsureSuccessStatusCode();

        (await UnpublishAsync(admin, listing.Id))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule 122. An owner whose listing disappears without a reason has nothing to act
    /// on — the same dead end rule 26 exists to close.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Unpublishing_without_a_note_is_refused(string? note)
    {
        var (_, admin, listing) = await PublishedAsync($"unpublish-note-{note?.Length ?? 0}");

        var response = await UnpublishAsync(admin, listing.Id, note);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    /// <summary>Rule 70: the owner is told, and told why.</summary>
    [Fact]
    public async Task The_owner_is_emailed_the_decision_and_the_reason()
    {
        var (owner, admin, listing) = await PublishedAsync("unpublish-email");

        await UnpublishAsync(admin, listing.Id, "The address does not match the property.");

        var decisions = _factory.Emails.DecisionsFor(owner.Email);

        decisions.ShouldContain(d =>
            d.Decision == "Taken off the site" &&
            d.Note == "The address does not match the property.");
    }

    [Fact]
    public async Task Nobody_but_an_admin_can_take_a_listing_down()
    {
        var (owner, _, listing) = await PublishedAsync("unpublish-forbidden");
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "unpublish-forbidden");

        (await owner.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/unpublish", new { AdminNote = "Mine." }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await seeker.Client.PostJsonAsync(
                $"/api/v1/admin/properties/{listing.Id}/unpublish", new { AdminNote = "Not mine." }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
