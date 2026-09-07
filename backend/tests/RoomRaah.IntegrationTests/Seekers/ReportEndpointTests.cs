using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Seekers;

[Collection(ApiCollection.Name)]
public class ReportEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public ReportEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(SeekerWorkspace Seeker, OwnerWorkspace Owner, AdminWorkspace Admin,
        OwnerPropertyDto Listing)> PublishedAsync(string label)
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, label);
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, label);

        return (seeker, owner, admin, listing);
    }

    private static object Report(int propertyId, string reason = "AlreadyTaken") => new
    {
        PropertyId = propertyId,
        Reason = reason,
        Details = "I visited on Tuesday and the owner said the room had gone a month ago."
    };

    [Fact]
    public async Task A_report_is_filed_open_and_reaches_the_admin_queue()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-file");

        var response = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var filed = await response.ReadAsync<MyReportDto>();
        filed.Status.ShouldBe(nameof(ReportStatus.Open));
        filed.Reason.ShouldBe(nameof(ReportReason.AlreadyTaken));

        var queue = await (await admin.Client.GetAsync("/api/v1/admin/reports?status=Open&pageSize=50"))
            .ReadAsync<PagedResult<ReportDto>>();

        var row = queue.Items.Single(r => r.Id == filed.Id);
        row.PropertyId.ShouldBe(listing.Id);
        row.PropertyTitle.ShouldBe(listing.Title);
        row.ReportedByDisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rule_42_a_second_unresolved_report_on_the_same_listing_is_refused()
    {
        var (seeker, _, _, listing) = await PublishedAsync("report-twice");

        (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .EnsureSuccessStatusCode();

        var again = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule 42 as amended in this phase. Before the amendment only Open blocked a
    /// second report, so a seeker could file again the moment an admin picked the
    /// first one up — which is exactly when the duplicate is least useful.
    /// </summary>
    [Fact]
    public async Task Rule_42_picking_a_report_up_does_not_open_the_door_to_another()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-under-review");

        var filed = await (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .ReadAsync<MyReportDto>();

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "UnderReview"
        })).EnsureSuccessStatusCode();

        var again = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_resolved_report_lets_the_same_person_report_again()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-resolved");

        var filed = await (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .ReadAsync<MyReportDto>();

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "Dismissed",
            AdminNote = "The owner confirmed the beds are free."
        })).EnsureSuccessStatusCode();

        var again = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Rule_97_a_verdict_without_a_note_is_refused()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-no-note");

        var filed = await (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .ReadAsync<MyReportDto>();

        var response = await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "Upheld"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_97_a_report_that_carries_a_verdict_is_not_decided_again()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-final");

        var filed = await (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .ReadAsync<MyReportDto>();

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "Upheld",
            AdminNote = "Confirmed with the owner."
        })).EnsureSuccessStatusCode();

        var again = await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "Dismissed",
            AdminNote = "Changed my mind."
        });

        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// Rule 98. A listing comes down because an admin decided to take it down, on the
    /// admin listing endpoints — not as a side effect of answering a complaint.
    /// </summary>
    [Fact]
    public async Task Rule_98_upholding_a_report_leaves_the_listing_published()
    {
        var (seeker, _, admin, listing) = await PublishedAsync("report-upheld");

        var filed = await (await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id)))
            .ReadAsync<MyReportDto>();

        (await admin.Client.PatchJsonAsync($"/api/v1/admin/reports/{filed.Id}", new
        {
            Status = "Upheld",
            AdminNote = "The room is gone."
        })).EnsureSuccessStatusCode();

        var stillThere = await _factory.CreateClient().GetAsync($"/api/v1/properties/{listing.Id}");

        stillThere.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rule_86_a_draft_cannot_be_reported()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "report-draft");
        var draft = await owner.CreateDraftAsync();
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "report-draft");

        var response = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(draft.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_reason_the_product_does_not_have_is_refused()
    {
        var (seeker, _, _, listing) = await PublishedAsync("report-reason");

        var response = await seeker.Client.PostJsonAsync(
            "/api/v1/reports", Report(listing.Id, reason: "Rude"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_report_queue_is_the_admins_alone()
    {
        var (seeker, owner, _, _) = await PublishedAsync("report-auth");

        (await _factory.CreateClient().GetAsync("/api/v1/admin/reports")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        (await seeker.Client.GetAsync("/api/v1/admin/reports")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        (await owner.Client.GetAsync("/api/v1/admin/reports")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The reporter is told what they filed and not what the review team wrote about
    /// it, and the admin queue names the reporter by display name alone.
    /// </summary>
    [Fact]
    public async Task Rule_33_neither_shape_carries_a_contact_detail()
    {
        var (seeker, owner, admin, listing) = await PublishedAsync("report-privacy");

        var response = await seeker.Client.PostJsonAsync("/api/v1/reports", Report(listing.Id));

        var filedBody = await response.Content.ReadAsStringAsync();

        filedBody.ShouldNotContain("adminNote");
        filedBody.ShouldNotContain("resolvedBy", Case.Insensitive);
        filedBody.ShouldNotContain(seeker.Email);

        var queueBody = await (await admin.Client.GetAsync("/api/v1/admin/reports?pageSize=50"))
            .Content.ReadAsStringAsync();

        queueBody.ShouldContain("reportedByDisplayName");
        queueBody.ShouldNotContain(seeker.Email);
        queueBody.ShouldNotContain(owner.Email);
        queueBody.ShouldNotContain("+9230011122");
        queueBody.ShouldNotContain("cnic", Case.Insensitive);
    }
}
