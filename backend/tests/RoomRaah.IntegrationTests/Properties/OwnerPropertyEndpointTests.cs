using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Properties;

[Collection(ApiCollection.Name)]
public class OwnerPropertyEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public OwnerPropertyEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private Task<OwnerWorkspace> OwnerAsync(string label) => OwnerWorkspace.CreateAsync(_factory, label);

    [Fact]
    public async Task Rule_14_a_new_listing_is_created_as_a_draft()
    {
        var owner = await OwnerAsync("create");
        var areaId = await OwnerWorkspace.AnyAreaIdAsync(owner.Client);

        var response = await owner.Client.PostJsonAsync(
            "/api/v1/owner/properties", OwnerWorkspace.Listing(areaId));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var listing = await response.ReadAsync<OwnerPropertyDto>();

        listing.Id.ShouldBeGreaterThan(0);
        listing.Status.ShouldBe("Draft");
        listing.Title.ShouldBe(OwnerWorkspace.FixtureTitle);
        listing.RoomType.ShouldBe("Shared");
        listing.GenderPolicy.ShouldBe("Boys");
        listing.Photos.ShouldBeEmpty();
        listing.AreaName.ShouldNotBeNullOrWhiteSpace();
        listing.CityName.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_created_listing_is_readable_at_the_location_that_was_returned()
    {
        var owner = await OwnerAsync("location");
        var areaId = await OwnerWorkspace.AnyAreaIdAsync(owner.Client);

        var response = await owner.Client.PostJsonAsync(
            "/api/v1/owner/properties", OwnerWorkspace.Listing(areaId));

        var location = response.Headers.Location.ShouldNotBeNull();

        var fetched = await (await owner.Client.GetAsync(location)).ReadAsync<OwnerPropertyDto>();

        fetched.Id.ShouldBe((await response.ReadAsync<OwnerPropertyDto>()).Id);
    }

    [Fact]
    public async Task The_facilities_that_were_chosen_come_back_on_the_listing()
    {
        var owner = await OwnerAsync("facilities");
        var facilities = await OwnerWorkspace.FacilitiesAsync(owner.Client);
        var chosen = facilities.Take(3).Select(f => f.Id).ToArray();

        var listing = await owner.CreateDraftAsync(null, chosen);

        listing.Facilities.Select(f => f.Id).OrderBy(id => id)
            .ShouldBe(chosen.OrderBy(id => id));
    }

    [Fact]
    public async Task A_listing_shows_up_on_the_owners_own_dashboard()
    {
        var owner = await OwnerAsync("dashboard");
        var listing = await owner.CreateDraftAsync();

        var page = await (await owner.Client.GetAsync("/api/v1/owner/properties"))
            .ReadAsync<PagedResult<OwnerPropertySummaryDto>>();

        page.Items.Select(i => i.Id).ShouldContain(listing.Id);
        page.TotalCount.ShouldBe(1);
        page.PageSize.ShouldBe(12);
    }

    [Fact]
    public async Task Rule_53_an_area_that_does_not_exist_is_refused()
    {
        var owner = await OwnerAsync("area");

        var response = await owner.Client.PostJsonAsync(
            "/api/v1/owner/properties", OwnerWorkspace.Listing(987654));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("area");
    }

    [Fact]
    public async Task Rule_53_a_facility_that_does_not_exist_is_refused()
    {
        var owner = await OwnerAsync("facility");
        var areaId = await OwnerWorkspace.AnyAreaIdAsync(owner.Client);

        var response = await owner.Client.PostJsonAsync(
            "/api/v1/owner/properties", OwnerWorkspace.Listing(areaId, 987654));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_17_a_listing_with_no_pin_is_refused_by_name()
    {
        var owner = await OwnerAsync("pin");
        var areaId = await OwnerWorkspace.AnyAreaIdAsync(owner.Client);

        var response = await owner.Client.PostJsonAsync("/api/v1/owner/properties", new
        {
            AreaId = areaId,
            Title = "Room with no pin",
            Description = "A room whose owner never dropped a marker on the map.",
            AddressLine = "House 1, Somewhere",
            RoomType = "Shared",
            GenderPolicy = "Boys",
            TotalBeds = 2,
            AvailableBeds = 1,
            MonthlyRent = 15000
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.ReadErrorAsync();

        error.Errors.ShouldNotBeNull();
        error.Errors!.Select(e => e.Field).ShouldContain("Latitude");
        error.Errors.Select(e => e.Field).ShouldContain("Longitude");
    }

    [Fact]
    public async Task Rule_16_more_free_beds_than_beds_is_refused()
    {
        var owner = await OwnerAsync("beds");
        var areaId = await OwnerWorkspace.AnyAreaIdAsync(owner.Client);

        var response = await owner.Client.PostJsonAsync("/api/v1/owner/properties", new
        {
            AreaId = areaId,
            Title = "Room with impossible arithmetic",
            Description = "Two beds, of which five are free.",
            AddressLine = "House 1, Somewhere",
            Latitude = 31.5,
            Longitude = 74.3,
            RoomType = "Shared",
            GenderPolicy = "Boys",
            TotalBeds = 2,
            AvailableBeds = 5,
            MonthlyRent = 15000
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!
            .Select(e => e.Field).ShouldContain("AvailableBeds");
    }

    [Fact]
    public async Task Every_owner_endpoint_refuses_a_caller_with_no_token()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/owner/properties"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/v1/owner/properties/1"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostJsonAsync("/api/v1/owner/properties", OwnerWorkspace.Listing(1)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync("/api/v1/owner/properties/1/submit", null))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rule_13_a_seeker_may_not_reach_the_owner_area()
    {
        var client = _factory.CreateClient();
        var session = await client.RegisterAndVerifyAsync(
            _factory, $"seeker.{Guid.NewGuid():N}@roomraah.local");
        client.Authorise(session.AccessToken);

        (await client.GetAsync("/api/v1/owner/properties"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostJsonAsync("/api/v1/owner/properties", OwnerWorkspace.Listing(1)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Rule 5. An unverified account may sign in and browse, but the owner area
    /// creates records, so the verified-account policy holds it out.
    /// </summary>
    [Fact]
    public async Task An_owner_who_has_not_verified_their_email_may_not_create_a_listing()
    {
        var client = _factory.CreateClient();
        var email = $"owner.unverified.{Guid.NewGuid():N}@roomraah.local";

        (await client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "Unverified Owner",
            Email = email,
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Owner",
            CnicNumber = "3520112345671"
        })).EnsureSuccessStatusCode();

        var session = await (await client.PostJsonAsync("/api/v1/auth/login", new
        {
            Email = email,
            Password = "Password1"
        })).ReadAsync<AuthResponseDto>();

        client.Authorise(session.AccessToken);

        (await client.GetAsync("/api/v1/owner/properties"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rule_18_one_owner_cannot_read_another_owners_listing()
    {
        var first = await OwnerAsync("first");
        var second = await OwnerAsync("second");

        var listing = await first.CreateDraftAsync();

        (await second.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The owner id is taken from the bearer token after model binding, so a query
    /// string that names somebody else is overwritten rather than obeyed.
    /// </summary>
    [Fact]
    public async Task An_owner_id_in_the_query_string_is_ignored()
    {
        var first = await OwnerAsync("victim");
        var second = await OwnerAsync("attacker");

        var listing = await first.CreateDraftAsync();

        var page = await (await second.Client.GetAsync("/api/v1/owner/properties?ownerId=1&page=1"))
            .ReadAsync<PagedResult<OwnerPropertySummaryDto>>();

        page.Items.ShouldBeEmpty();
        page.Items.Select(i => i.Id).ShouldNotContain(listing.Id);
    }

    [Fact]
    public async Task A_listing_that_does_not_exist_is_not_found()
    {
        var owner = await OwnerAsync("missing");

        (await owner.Client.GetAsync("/api/v1/owner/properties/987654"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=51")]
    public async Task Rule_30_a_page_outside_the_allowed_range_is_refused(string query)
    {
        var owner = await OwnerAsync("paging");

        var response = await owner.Client.GetAsync($"/api/v1/owner/properties{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rule_15_a_listing_with_no_photos_cannot_be_submitted()
    {
        var owner = await OwnerAsync("nophotos");
        var listing = await owner.CreateDraftAsync();

        var response = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/submit", null);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("3");
    }

    [Fact]
    public async Task Rule_52_a_listing_already_in_the_queue_cannot_be_submitted_again()
    {
        var owner = await OwnerAsync("resubmit");
        var listing = await owner.CreateSubmittableAsync();

        (await owner.Client.PostAsync($"/api/v1/owner/properties/{listing.Id}/submit", null))
            .EnsureSuccessStatusCode();

        var second = await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/submit", null);

        second.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_18_another_owner_cannot_submit_a_listing()
    {
        var first = await OwnerAsync("owner-a");
        var second = await OwnerAsync("owner-b");

        var listing = await first.CreateSubmittableAsync();

        (await second.Client.PostAsync($"/api/v1/owner/properties/{listing.Id}/submit", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The end of Phase 3: a complete listing with photographs and a pin, sitting
    /// in the review queue and visible to nobody else.
    /// </summary>
    [Fact]
    public async Task A_complete_listing_reaches_the_review_queue()
    {
        var owner = await OwnerAsync("journey");
        var facilities = await OwnerWorkspace.FacilitiesAsync(owner.Client);

        var listing = await owner.CreateDraftAsync(null, facilities.Take(2).Select(f => f.Id).ToArray());

        await owner.UploadAsync(listing.Id);
        await owner.UploadAsync(listing.Id);
        await owner.UploadAsync(listing.Id);

        var submitted = await (await owner.Client.PostAsync(
            $"/api/v1/owner/properties/{listing.Id}/submit", null)).ReadAsync<OwnerPropertyDto>();

        submitted.Status.ShouldBe("PendingReview");
        submitted.Photos.Count.ShouldBe(3);
        submitted.Photos.Count(p => p.IsPrimary).ShouldBe(1);
        submitted.Facilities.Count.ShouldBe(2);
        submitted.Latitude.ShouldBe(OwnerWorkspace.FixtureLatitude);

        var stranger = await OwnerAsync("stranger");
        (await stranger.Client.GetAsync($"/api/v1/owner/properties/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
