using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Properties;

[Collection(ApiCollection.Name)]
public class OwnerPhotoEndpointTests
{
    private const int ThumbnailLongEdge = 400;

    private readonly RoomRaahApiFactory _factory;

    public OwnerPhotoEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private Task<OwnerWorkspace> OwnerAsync(string label) => OwnerWorkspace.CreateAsync(_factory, label);

    [Fact]
    public async Task A_photograph_is_stored_and_answered_with_both_of_its_urls()
    {
        var owner = await OwnerAsync("upload");
        var listing = await owner.CreateDraftAsync();

        using var content = TestImages.AsUpload(TestImages.Jpeg());
        var response = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var photo = await response.ReadAsync<PropertyPhotoDto>();

        photo.Url.ShouldNotBeNullOrWhiteSpace();
        photo.ThumbnailUrl.ShouldNotBeNullOrWhiteSpace();
        photo.Url.ShouldNotBe(photo.ThumbnailUrl);
        photo.Status.ShouldBe("Live");
        photo.IsPrimary.ShouldBeTrue();
        photo.SortOrder.ShouldBe(1);
    }

    [Fact]
    public async Task The_stored_original_is_the_size_it_was_uploaded_at()
    {
        var owner = await OwnerAsync("original");
        var listing = await owner.CreateDraftAsync();

        var photo = await owner.UploadAsync(listing.Id, TestImages.Jpeg(1200, 800));

        var stored = await TestImages.MeasureAsync(photo.Url);

        stored.Width.ShouldBe(1200);
        stored.Height.ShouldBe(800);
        stored.ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>
    /// The reason thumbnails exist at all: a search page shows a dozen cards, and a
    /// dozen full-size photographs is a page that never finishes loading on mobile
    /// data. This reads the file back out of storage rather than trusting the URL.
    /// </summary>
    [Fact]
    public async Task The_thumbnail_is_reduced_to_four_hundred_pixels_on_its_long_edge()
    {
        var owner = await OwnerAsync("thumb");
        var listing = await owner.CreateDraftAsync();

        var photo = await owner.UploadAsync(listing.Id, TestImages.Jpeg(1200, 800));

        var thumbnail = await TestImages.MeasureAsync(photo.ThumbnailUrl);

        Math.Max(thumbnail.Width, thumbnail.Height).ShouldBe(ThumbnailLongEdge);
        thumbnail.Width.ShouldBe(400);
        thumbnail.Height.ShouldBe(267);
    }

    [Fact]
    public async Task A_tall_photograph_is_reduced_on_its_own_long_edge()
    {
        var owner = await OwnerAsync("tall");
        var listing = await owner.CreateDraftAsync();

        var photo = await owner.UploadAsync(listing.Id, TestImages.Jpeg(600, 1500));

        var thumbnail = await TestImages.MeasureAsync(photo.ThumbnailUrl);

        thumbnail.Height.ShouldBe(ThumbnailLongEdge);
        thumbnail.Width.ShouldBeLessThan(ThumbnailLongEdge);
    }

    [Fact]
    public async Task A_photograph_smaller_than_the_thumbnail_is_not_enlarged()
    {
        var owner = await OwnerAsync("small");
        var listing = await owner.CreateDraftAsync();

        var photo = await owner.UploadAsync(listing.Id, TestImages.Jpeg(200, 150));

        var thumbnail = await TestImages.MeasureAsync(photo.ThumbnailUrl);

        thumbnail.Width.ShouldBe(200);
        thumbnail.Height.ShouldBe(150);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task All_three_accepted_formats_are_stored_and_thumbnailed_as_jpeg(string format)
    {
        var owner = await OwnerAsync($"format-{format}");
        var listing = await owner.CreateDraftAsync();

        var bytes = format == "png" ? TestImages.Png() : TestImages.Webp();
        var contentType = $"image/{format}";

        var photo = await owner.UploadAsync(listing.Id, bytes, $"room.{format}", contentType);

        (await TestImages.MeasureAsync(photo.Url)).ContentType.ShouldBe(contentType);
        (await TestImages.MeasureAsync(photo.ThumbnailUrl)).ContentType.ShouldBe("image/jpeg");
    }

    /// <summary>
    /// Rule 56. The declared content type says JPEG and the bytes are a text file:
    /// a client controls its header exactly as easily as its file extension, so the
    /// check that counts is the one that reads the file.
    /// </summary>
    [Fact]
    public async Task Rule_56_a_file_that_only_claims_to_be_a_photograph_is_refused()
    {
        var owner = await OwnerAsync("liar");
        var listing = await owner.CreateDraftAsync();

        using var content = TestImages.AsUpload(TestImages.NotAnImage(), "room.jpg", "image/jpeg");
        var response = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos", content);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("JPEG");

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_content_type_that_is_not_an_accepted_one_is_refused_as_bad_input()
    {
        var owner = await OwnerAsync("pdf");
        var listing = await owner.CreateDraftAsync();

        using var content = TestImages.AsUpload(TestImages.Jpeg(), "room.pdf", "application/pdf");
        var response = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_request_carrying_no_file_at_all_is_refused_as_bad_input()
    {
        var owner = await OwnerAsync("nofile");
        var listing = await owner.CreateDraftAsync();

        using var empty = new MultipartFormDataContent();
        var response = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos", empty);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rule_57_the_first_photograph_is_primary_and_later_ones_are_not()
    {
        var owner = await OwnerAsync("primary");
        var listing = await owner.CreateDraftAsync();

        var first = await owner.UploadAsync(listing.Id);
        var second = await owner.UploadAsync(listing.Id);

        first.IsPrimary.ShouldBeTrue();
        second.IsPrimary.ShouldBeFalse();
        second.SortOrder.ShouldBe(2);
    }

    [Fact]
    public async Task Rule_57_the_owner_can_choose_a_different_primary_photograph()
    {
        var owner = await OwnerAsync("choose");
        var listing = await owner.CreateDraftAsync();

        await owner.UploadAsync(listing.Id);
        var second = await owner.UploadAsync(listing.Id);

        var result = await (await owner.Client.PatchAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/{second.Id}/primary", null))
            .ReadAsync<OwnerListingUpdateDto>();

        result.PendingRevision.ShouldBeNull();
        result.Listing.Photos.Count(p => p.IsPrimary).ShouldBe(1);
        result.Listing.Photos.Single(p => p.IsPrimary).Id.ShouldBe(second.Id);
    }

    [Fact]
    public async Task A_photograph_that_is_not_on_the_listing_cannot_be_made_primary()
    {
        var owner = await OwnerAsync("stray");
        var listing = await owner.CreateDraftAsync();
        await owner.UploadAsync(listing.Id);

        (await owner.Client.PatchAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/987654/primary", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rule_58_the_gallery_order_is_set_by_naming_every_photograph()
    {
        var owner = await OwnerAsync("reorder");
        var listing = await owner.CreateDraftAsync();

        var one = await owner.UploadAsync(listing.Id);
        var two = await owner.UploadAsync(listing.Id);
        var three = await owner.UploadAsync(listing.Id);

        var reordered = await (await owner.Client.PatchJsonAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/order",
                new { PhotoIds = new[] { three.Id, one.Id, two.Id } }))
            .ReadAsync<OwnerListingUpdateDto>();

        reordered.PendingRevision.ShouldBeNull();
        reordered.Listing.Photos.Select(p => p.Id).ShouldBe([three.Id, one.Id, two.Id]);
        reordered.Listing.Photos.Select(p => p.SortOrder).ShouldBe([1, 2, 3]);

        var fetched = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        fetched.Photos.Select(p => p.Id).ShouldBe([three.Id, one.Id, two.Id]);
    }

    [Fact]
    public async Task Rule_58_an_order_that_leaves_a_photograph_out_is_refused()
    {
        var owner = await OwnerAsync("partial");
        var listing = await owner.CreateDraftAsync();

        var one = await owner.UploadAsync(listing.Id);
        await owner.UploadAsync(listing.Id);

        var response = await owner.Client.PatchJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos/order",
            new { PhotoIds = new[] { one.Id } });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("every photo");
    }

    [Fact]
    public async Task An_empty_order_is_refused_as_bad_input()
    {
        var owner = await OwnerAsync("emptyorder");
        var listing = await owner.CreateDraftAsync();
        await owner.UploadAsync(listing.Id);

        var response = await owner.Client.PatchJsonAsync(
            $"/api/v1/owner/properties/{listing.Id}/photos/order",
            new { PhotoIds = Array.Empty<int>() });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_a_photograph_removes_the_row_and_the_stored_files()
    {
        var owner = await OwnerAsync("delete");
        var listing = await owner.CreateDraftAsync();

        var first = await owner.UploadAsync(listing.Id);
        await owner.UploadAsync(listing.Id);

        (await owner.Client.DeleteAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/{first.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Select(p => p.Id).ShouldNotContain(first.Id);

        using var http = new HttpClient();
        (await http.GetAsync(first.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await http.GetAsync(first.ThumbnailUrl)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rule_57_deleting_the_primary_photograph_promotes_the_next_one()
    {
        var owner = await OwnerAsync("promote");
        var listing = await owner.CreateDraftAsync();

        var first = await owner.UploadAsync(listing.Id);
        var second = await owner.UploadAsync(listing.Id);

        await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{first.Id}");

        var after = await (await owner.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .ReadAsync<OwnerPropertyDto>();

        after.Photos.Count(p => p.IsPrimary).ShouldBe(1);
        after.Photos.Single(p => p.IsPrimary).Id.ShouldBe(second.Id);
    }

    [Fact]
    public async Task A_photograph_that_is_not_on_the_listing_cannot_be_deleted()
    {
        var owner = await OwnerAsync("deletestray");
        var listing = await owner.CreateDraftAsync();

        (await owner.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/987654"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rule_18_another_owner_cannot_touch_the_photographs()
    {
        var first = await OwnerAsync("photos-a");
        var second = await OwnerAsync("photos-b");

        var listing = await first.CreateDraftAsync();
        var photo = await first.UploadAsync(listing.Id);

        using var content = TestImages.AsUpload(TestImages.Jpeg());

        (await second.Client.PostAsync($"/api/v1/owner/properties/{listing.Id}/photos", content))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await second.Client.DeleteAsync($"/api/v1/owner/properties/{listing.Id}/photos/{photo.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await second.Client.PatchAsync(
                $"/api/v1/owner/properties/{listing.Id}/photos/{photo.Id}/primary", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
