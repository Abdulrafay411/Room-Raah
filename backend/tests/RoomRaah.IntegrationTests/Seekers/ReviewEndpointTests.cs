using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Seekers;

/// <summary>
/// The journey rule 38 exists for: ask, be accepted, go, say you went, and only then
/// write. And the queue rule 41 needs to be a rule rather than a wall.
/// </summary>
[Collection(ApiCollection.Name)]
public class ReviewEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public ReviewEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<(SeekerWorkspace Seeker, OwnerWorkspace Owner, AdminWorkspace Admin,
        OwnerPropertyDto Listing)> PublishedAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, label);

        return (seeker, owner, admin, listing);
    }

    private async Task<PropertyReviewsDto> PublicReviewsAsync(int propertyId)
    {
        var response = await Anonymous().GetAsync($"/api/v1/properties/{propertyId}/reviews");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadAsync<PropertyReviewsDto>();
    }

    private async Task ApproveAsync(AdminWorkspace admin, int reviewId)
    {
        var response = await admin.Client.PatchJsonAsync(
            $"/api/v1/admin/reviews/{reviewId}", new { State = "Approved" });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_whole_journey_from_a_visit_to_a_review_a_stranger_can_read()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("review-journey");

        var before = await PublicReviewsAsync(listing.Id);
        before.Items.ShouldBeEmpty();
        before.AverageRating.ShouldBeNull();

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        var written = await seeker.ReviewAsync(listing.Id, rating: 5, stayStatus: "CurrentlyStaying");

        // Rule 41: written, and invisible.
        written.ModerationState.ShouldBe(nameof(ModerationState.Pending));
        (await PublicReviewsAsync(listing.Id)).Items.ShouldBeEmpty();

        await ApproveAsync(admin, written.Id);

        var after = await PublicReviewsAsync(listing.Id);

        after.TotalCount.ShouldBe(1);
        after.AverageRating.ShouldBe(5d);

        var review = after.Items.Single();
        review.Rating.ShouldBe(5);
        review.StayStatus.ShouldBe(nameof(StayStatus.CurrentlyStaying));
        review.AuthorDisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Rule 38. The point of the whole visit lifecycle: without a completed visit
    /// there is nothing to say a reviewer ever saw the room.
    /// </summary>
    [Fact]
    public async Task Rule_38_a_review_without_a_completed_visit_is_refused()
    {
        var (seeker, _, _, listing) = await PublishedAsync("review-no-visit");

        var response = await seeker.Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = listing.Id,
            Rating = 5,
            Comment = "Never saw it, looks great.",
            StayStatus = "Visited"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule 38 again, and the half that matters: an accepted visit is not a completed
    /// one, so the gate does not open when the owner agrees — only when somebody says
    /// the visit actually happened.
    /// </summary>
    [Fact]
    public async Task Rule_38_an_accepted_visit_is_not_yet_a_completed_one()
    {
        var (seeker, owner, _, listing) = await PublishedAsync("review-accepted-only");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted"
        })).EnsureSuccessStatusCode();

        var response = await seeker.Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = listing.Id,
            Rating = 4,
            Comment = "Booked to see it next week.",
            StayStatus = "Visited"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_39_a_second_review_of_the_same_listing_is_refused()
    {
        var (seeker, owner, _, listing) = await PublishedAsync("review-twice");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);
        await seeker.ReviewAsync(listing.Id);

        var again = await seeker.Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = listing.Id,
            Rating = 1,
            Comment = "Changed my mind.",
            StayStatus = "Visited"
        });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Rule_40_a_rating_off_the_scale_is_refused(int rating)
    {
        var (seeker, owner, _, listing) = await PublishedAsync($"review-rating-{rating}");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        var response = await seeker.Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = listing.Id,
            Rating = rating,
            Comment = "A comment.",
            StayStatus = "Visited"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_admin_can_hide_a_review_that_was_already_public()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("review-hide");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        var written = await seeker.ReviewAsync(listing.Id);

        await ApproveAsync(admin, written.Id);
        (await PublicReviewsAsync(listing.Id)).TotalCount.ShouldBe(1);

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/reviews/{written.Id}", new
        {
            State = "Hidden"
        })).EnsureSuccessStatusCode();

        (await PublicReviewsAsync(listing.Id)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Rule_96_a_review_cannot_be_pushed_back_to_pending()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("review-back");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        var written = await seeker.ReviewAsync(listing.Id);

        var response = await admin.Client.PatchJsonAsync(
            $"/api/v1/admin/reviews/{written.Id}", new { State = "Pending" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_moderation_queue_is_the_admins_alone()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("review-queue-auth");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        var written = await seeker.ReviewAsync(listing.Id);

        (await Anonymous().GetAsync("/api/v1/admin/reviews")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        (await seeker.Client.GetAsync("/api/v1/admin/reviews")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await owner.Client.PatchJsonAsync($"/api/v1/admin/reviews/{written.Id}", new
        {
            State = "Approved"
        })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var queue = await (await admin.Client.GetAsync("/api/v1/admin/reviews?state=Pending&pageSize=50"))
            .ReadAsync<PagedResult<AdminReviewDto>>();

        queue.Items.Select(r => r.Id).ShouldContain(written.Id);
        queue.Items.ShouldAllBe(r => r.ModerationState == "Pending");
    }

    [Fact]
    public async Task Rule_94_the_average_is_taken_over_every_approved_review()
    {
        var (first, owner, admin, listing) = await PublishedAsync("review-average");
        var second = await SeekerWorkspace.CreateAsync(_factory, "review-average-two");

        await first.CompleteVisitAsync(_factory, owner, listing.Id);
        await second.CompleteVisitAsync(_factory, owner, listing.Id);

        await ApproveAsync(admin, (await first.ReviewAsync(listing.Id, rating: 5)).Id);
        await ApproveAsync(admin, (await second.ReviewAsync(listing.Id, rating: 2)).Id);

        var page = await PublicReviewsAsync(listing.Id);

        page.TotalCount.ShouldBe(2);
        page.AverageRating.ShouldBe(3.5d);
    }

    [Fact]
    public async Task Rule_28_the_reviews_of_a_draft_are_not_found()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "review-draft");
        var draft = await owner.CreateDraftAsync();

        (await Anonymous().GetAsync($"/api/v1/properties/{draft.Id}/reviews")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Rule 95 read off the wire. A review is written by one visitor and read by every
    /// other, so the shape is asserted on the raw body rather than on the DTO.
    /// </summary>
    [Fact]
    public async Task Rule_95_a_public_review_names_its_author_and_nothing_else()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("review-privacy");

        await seeker.CompleteVisitAsync(_factory, owner, listing.Id);

        await ApproveAsync(admin, (await seeker.ReviewAsync(listing.Id)).Id);

        var body = await (await Anonymous().GetAsync($"/api/v1/properties/{listing.Id}/reviews"))
            .Content.ReadAsStringAsync();

        body.ShouldContain("authorDisplayName");
        body.ShouldNotContain(seeker.Email);
        body.ShouldNotContain(owner.Email);
        body.ShouldNotContain("authorId");
        body.ShouldNotContain("moderationState");
        body.ShouldNotContain("moderatedBy", Case.Insensitive);
        body.ShouldNotContain("+9230011122");
    }

    [Fact]
    public async Task An_owner_cannot_write_a_review()
    {
        var (_, owner, _, listing) = await PublishedAsync("review-owner");

        var response = await owner.Client.PostJsonAsync("/api/v1/reviews", new
        {
            PropertyId = listing.Id,
            Rating = 5,
            Comment = "My own room is excellent.",
            StayStatus = "Visited"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
