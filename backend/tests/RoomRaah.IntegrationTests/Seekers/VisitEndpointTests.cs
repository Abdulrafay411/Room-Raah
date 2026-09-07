using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Seekers;

[Collection(ApiCollection.Name)]
public class VisitEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public VisitEndpointTests(RoomRaahApiFactory factory)
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
    public async Task A_seeker_asks_and_the_owner_accepts()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-accept");

        var visit = await seeker.RequestVisitAsync(listing.Id, "Video");

        visit.Status.ShouldBe(nameof(VisitStatus.Requested));
        visit.VisitType.ShouldBe(nameof(VisitType.Video));
        visit.PropertyTitle.ShouldBe(listing.Title);

        var queue = await (await owner.Client.GetAsync("/api/v1/owner/visits"))
            .ReadAsync<PagedResult<VisitRequestDto>>();

        queue.Items.Select(v => v.Id).ShouldContain(visit.Id);

        var answered = await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        });

        answered.StatusCode.ShouldBe(HttpStatusCode.OK);

        var dto = await answered.ReadAsync<VisitRequestDto>();
        dto.Status.ShouldBe(nameof(VisitStatus.Accepted));
        dto.OwnerResponseNote.ShouldBe("Come after 4pm.");

        var mine = await (await seeker.Client.GetAsync("/api/v1/visits/my"))
            .ReadAsync<PagedResult<VisitRequestDto>>();

        mine.Items.Single(v => v.Id == visit.Id).Status.ShouldBe(nameof(VisitStatus.Accepted));
    }

    /// <summary>Rule 92: the email is the only thing that tells an owner to log in.</summary>
    [Fact]
    public async Task Rule_92_the_owner_is_emailed_and_then_the_seeker_is()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-emails");

        var before = _factory.Emails.DecisionsFor(owner.Email).Length;

        var visit = await seeker.RequestVisitAsync(listing.Id);

        _factory.Emails.DecisionsFor(owner.Email).Length.ShouldBe(before + 1);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Declined",
            OwnerResponseNote = "Both beds are taken."
        })).EnsureSuccessStatusCode();

        var toSeeker = _factory.Emails.DecisionsFor(seeker.Email);

        toSeeker.ShouldContain(d => d.Decision == nameof(VisitStatus.Declined) &&
                                    d.Note == "Both beds are taken.");
    }

    [Fact]
    public async Task Rule_36_a_second_open_request_is_refused()
    {
        var (seeker, _, listing) = await PublishedAsync("visit-double");

        await seeker.RequestVisitAsync(listing.Id);

        var again = await seeker.Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = listing.Id,
            VisitType = "Physical",
            PreferredAt = DateTime.UtcNow.AddDays(5)
        });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule 36 names two open states, and the accepted one is the half a careless
    /// check drops: a seeker whose visit is already arranged asking for a second is
    /// the case the rule exists for.
    /// </summary>
    [Fact]
    public async Task Rule_36_an_accepted_request_also_blocks_a_second_one()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-accepted-blocks");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        })).EnsureSuccessStatusCode();

        var again = await seeker.Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = listing.Id,
            VisitType = "Video",
            PreferredAt = DateTime.UtcNow.AddDays(9)
        });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>Rule 36: a declined request is finished business, so asking again is allowed.</summary>
    [Fact]
    public async Task Rule_36_a_declined_request_does_not_block_the_next_one()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-again");

        var first = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{first.Id}/respond", new
        {
            Status = "Declined",
            OwnerResponseNote = "Taken this month."
        })).EnsureSuccessStatusCode();

        var second = await seeker.Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = listing.Id,
            VisitType = "Physical",
            PreferredAt = DateTime.UtcNow.AddDays(20)
        });

        second.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rule_37_another_owner_cannot_answer_a_request()
    {
        var (seeker, _, listing) = await PublishedAsync("visit-not-yours");
        var stranger = await OwnerWorkspace.CreateAsync(_factory, "visit-stranger");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        var response = await stranger.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rule_89_declining_without_a_reason_is_refused()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-no-reason");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        var response = await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Declined"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_88_only_the_two_answers_may_be_named()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-bad-answer");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        var response = await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Completed"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_90_a_visit_whose_time_has_not_come_cannot_be_completed()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-too-soon");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted"
        })).EnsureSuccessStatusCode();

        var response = await seeker.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/complete", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_90_the_owner_may_also_say_the_visit_happened()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-owner-completes");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted"
        })).EnsureSuccessStatusCode();

        await SeekerWorkspace.BackdateAsync(_factory, visit.Id);

        var response = await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/complete", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<VisitRequestDto>()).Status.ShouldBe(nameof(VisitStatus.Completed));
    }

    [Fact]
    public async Task Rule_90_somebody_who_was_not_there_cannot_complete_it()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-outsider");
        var outsider = await SeekerWorkspace.CreateAsync(_factory, "visit-outsider-other");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted"
        })).EnsureSuccessStatusCode();

        await SeekerWorkspace.BackdateAsync(_factory, visit.Id);

        var response = await outsider.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/complete", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rule_91_the_seeker_cancels_and_the_owner_cannot()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-cancel");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        var byOwner = await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/cancel", new { });

        // The owner does not hold the Seeker role at all, so the route refuses before
        // the handler is reached. Either answer is a refusal; what matters is that an
        // owner cannot make a withdrawal look like the seeker's own doing.
        byOwner.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);

        var bySeeker = await seeker.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/cancel", new { });

        bySeeker.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await bySeeker.ReadAsync<VisitRequestDto>()).Status.ShouldBe(nameof(VisitStatus.Cancelled));
    }

    [Fact]
    public async Task Rule_86_a_visit_cannot_be_requested_on_a_draft()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "visit-draft");
        var draft = await owner.CreateDraftAsync();
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "visit-draft");

        var response = await seeker.Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = draft.Id,
            VisitType = "Physical",
            PreferredAt = DateTime.UtcNow.AddDays(2)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_preferred_time_in_the_past_is_refused()
    {
        var (seeker, _, listing) = await PublishedAsync("visit-past");

        var response = await seeker.Client.PostJsonAsync("/api/v1/visits", new
        {
            PropertyId = listing.Id,
            VisitType = "Physical",
            PreferredAt = DateTime.UtcNow.AddDays(-1)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Rule 93 read off the wire. This is the first place in the product where one
    /// user's details are handed to another, so it is asserted on the raw body rather
    /// than on the DTO: a field added to the shape would slip past a typed check.
    /// </summary>
    [Fact]
    public async Task Rule_93_neither_side_is_given_the_others_contact_details()
    {
        var (seeker, owner, listing) = await PublishedAsync("visit-privacy");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        })).EnsureSuccessStatusCode();

        var ownerSees = await (await owner.Client.GetAsync("/api/v1/owner/visits"))
            .Content.ReadAsStringAsync();

        ownerSees.ShouldContain("seekerDisplayName");
        ownerSees.ShouldNotContain(seeker.Email);
        ownerSees.ShouldNotContain("+9230011122");

        var seekerSees = await (await seeker.Client.GetAsync("/api/v1/visits/my"))
            .Content.ReadAsStringAsync();

        seekerSees.ShouldNotContain(owner.Email);
        seekerSees.ShouldNotContain("+9230011122");
        seekerSees.ShouldNotContain("ownerDisplayName");
        seekerSees.ShouldNotContain("cnic", Case.Insensitive);
    }

    [Fact]
    public async Task An_owner_cannot_read_another_owners_visit_requests()
    {
        var (seeker, _, listing) = await PublishedAsync("visit-owner-isolation");
        var stranger = await OwnerWorkspace.CreateAsync(_factory, "visit-owner-isolation-other");

        var visit = await seeker.RequestVisitAsync(listing.Id);

        var theirs = await (await stranger.Client.GetAsync("/api/v1/owner/visits?pageSize=50"))
            .ReadAsync<PagedResult<VisitRequestDto>>();

        theirs.Items.Select(v => v.Id).ShouldNotContain(visit.Id);
    }
}
