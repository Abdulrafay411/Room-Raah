using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.AdminListings;

/// <summary>
/// Photographs on a published listing, which are proposals rather than changes. The
/// files matter as much as the rows here: a photograph that loses has a blob and a
/// thumbnail behind it, and both have to be gone when the row is.
/// </summary>
[Collection(ApiCollection.Name)]
public class PublishedPhotoEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public PublishedPhotoEndpointTests(RoomRaahApiFactory factory)
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

    private async Task<RevisionDetailDto> PendingContentAsync(AdminWorkspace admin, int propertyId)
    {
        var queue = await (await admin.Client.GetAsync("/api/v1/admin/revisions?kind=Content&pageSize=50"))
            .ReadAsync<PagedResult<RevisionQueueRowDto>>();

        var row = queue.Items.FirstOrDefault(r => r.PropertyId == propertyId)
            ?? throw new InvalidOperationException($"No content revision is pending on {propertyId}.");

        return await (await admin.Client.GetAsync($"/api/v1/admin/revisions/{row.Id}"))
            .ReadAsync<RevisionDetailDto>();
    }

    private static async Task<HttpStatusCode> FetchAsync(string url)
    {
        using var plain = new HttpClient();

        return (await plain.GetAsync(url)).StatusCode;
    }

    [Fact]
    public async Task Rule_66_a_photograph_added_to_a_published_listing_waits_for_an_admin()
    {
        var (owner, admin, listing) = await Published("addphoto");

        var added = await owner.UploadAsync(listing.Id);

        added.Status.ShouldBe("PendingAdd");
        added.IsPrimary.ShouldBeFalse();

        var revision = await PendingContentAsync(admin, listing.Id);

        revision.PhotosToAdd.Select(p => p.Id).ShouldBe([added.Id]);
        revision.CurrentPhotos.Count.ShouldBe(3);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/approve", new { });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count.ShouldBe(4);
        after.Photos.ShouldAllBe(p => p.Status == "Live");
        after.Photos.Count(p => p.IsPrimary).ShouldBe(1);

        // The file is still there, and still reachable.
        (await FetchAsync(added.Url)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rule_69_rejecting_deletes_the_proposed_photograph_and_its_files()
    {
        var (owner, admin, listing) = await Published("rejectphoto");

        var added = await owner.UploadAsync(listing.Id);

        (await FetchAsync(added.Url)).ShouldBe(HttpStatusCode.OK);

        var revision = await PendingContentAsync(admin, listing.Id);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/reject",
            new { AdminNote = "That photograph is of a different room." });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count.ShouldBe(3);
        after.Photos.ShouldNotContain(p => p.Id == added.Id);

        (await FetchAsync(added.Url)).ShouldBe(HttpStatusCode.NotFound);
        (await FetchAsync(added.ThumbnailUrl)).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rule_66_a_deletion_on_a_published_listing_leaves_the_photograph_on_the_page()
    {
        var (owner, admin, listing) = await Published("removephoto");

        var doomed = listing.Photos.Last();

        (await owner.Client.DeleteAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/{doomed.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var stillThere = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        stillThere.Photos.Count.ShouldBe(3);
        stillThere.Photos.Single(p => p.Id == doomed.Id).Status.ShouldBe("PendingRemove");
        (await FetchAsync(doomed.Url)).ShouldBe(HttpStatusCode.OK);

        var revision = await PendingContentAsync(admin, listing.Id);

        revision.PhotosToRemove.Select(p => p.Id).ShouldBe([doomed.Id]);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/approve", new { });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count.ShouldBe(2);
        after.Photos.Select(p => p.SortOrder).ShouldBe([1, 2]);
        after.Photos.Count(p => p.IsPrimary).ShouldBe(1);

        (await FetchAsync(doomed.Url)).ShouldBe(HttpStatusCode.NotFound);
        (await FetchAsync(doomed.ThumbnailUrl)).ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Rule 69 the other way round: a removal an admin refuses puts the photograph
    /// back on the page rather than leaving it in limbo.
    /// </summary>
    [Fact]
    public async Task Rejecting_a_removal_returns_the_photograph_to_the_gallery()
    {
        var (owner, admin, listing) = await Published("keepphoto");

        var doomed = listing.Photos.Last();

        await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{doomed.Id}");

        var revision = await PendingContentAsync(admin, listing.Id);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/reject",
            new { AdminNote = "Keep it — it is the only photograph of the bathroom." });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count.ShouldBe(3);
        after.Photos.ShouldAllBe(p => p.Status == "Live");
        (await FetchAsync(doomed.Url)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rule_68_the_proposed_order_and_primary_are_applied_together_on_approval()
    {
        var (owner, admin, listing) = await Published("gallery");

        var ids = listing.Photos.Select(p => p.Id).ToList();
        var wanted = new[] { ids[2], ids[0], ids[1] };

        var reordered = await (await owner.Client.PatchJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/order",
                new { PhotoIds = wanted }))
            .ReadAsync<OwnerListingUpdateDto>();

        // Rule 19: the published gallery has not moved.
        reordered.Listing.Photos.Select(p => p.Id).ShouldBe(ids);
        reordered.PendingRevision.ShouldNotBeNull().ProposedPhotoOrder.ShouldBe(wanted);

        await owner.Client.PatchAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos/{ids[1]}/primary", null);

        var revision = await PendingContentAsync(admin, listing.Id);

        revision.ProposedPhotoOrder.ShouldBe(wanted);

        await admin.Client.PostJsonAsync($"/api/v1/admin/revisions/{revision.Id}/approve", new { });

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Select(p => p.Id).ShouldBe(wanted);
        after.Photos.Single(p => p.IsPrimary).Id.ShouldBe(ids[1]);
    }

    [Fact]
    public async Task Rule_67_every_photo_change_joins_one_revision_rather_than_opening_several()
    {
        var (owner, admin, listing) = await Published("onequeue");

        await owner.UploadAsync(listing.Id);
        await owner.Client.DeleteAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos/{listing.Photos[0].Id}");
        await owner.Client.PatchAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos/{listing.Photos[1].Id}/primary", null);

        var queue = await (await admin.Client.GetAsync("/api/v1/admin/revisions?kind=Content&pageSize=50"))
            .ReadAsync<PagedResult<RevisionQueueRowDto>>();

        queue.Items.Count(r => r.PropertyId == listing.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Withdrawing_a_proposed_photograph_deletes_it_outright()
    {
        var (owner, _, listing) = await Published("withdraw");

        var added = await owner.UploadAsync(listing.Id);

        (await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{added.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count.ShouldBe(3);
        (await FetchAsync(added.Url)).ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_photograph_already_waiting_to_be_removed_cannot_be_removed_again()
    {
        var (owner, _, listing) = await Published("twice");

        var doomed = listing.Photos.Last();

        await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{doomed.Id}");

        (await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{doomed.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
