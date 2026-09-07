using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Search;

/// <summary>
/// The search endpoint against a real database, with the haversine running in SQL
/// Server rather than in memory. The seeder writes eight published listings across
/// three cities, so these assert against real data rather than a fixture built here.
/// </summary>
[Collection(ApiCollection.Name)]
public class PublicSearchEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public PublicSearchEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>No token, ever. Browsing is public.</summary>
    private HttpClient Anonymous() => _factory.CreateClient();

    private async Task<PagedResult<ListingCardDto>> SearchAsync(string query = "")
    {
        var response = await Anonymous().GetAsync("/api/v1/properties" + query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadAsync<PagedResult<ListingCardDto>>();
    }

    private async Task<int> LandmarkIdAsync(string name)
    {
        var suggestions = await (await Anonymous().GetAsync($"/api/v1/locations/suggest?q={name}"))
            .ReadAsync<List<LocationSuggestionDto>>();

        return suggestions.First(s => s.Type == LocationSuggestionDto.LandmarkType).Id;
    }

    [Fact]
    public async Task A_stranger_with_no_token_can_search()
    {
        var page = await SearchAsync("?pageSize=50");

        page.Items.ShouldNotBeEmpty();
        page.TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Rule_28_a_listing_that_has_not_been_published_never_appears()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "search-draft");
        var draft = await owner.CreateDraftAsync();

        var page = await SearchAsync("?pageSize=50");

        page.Items.ShouldNotContain(card => card.Id == draft.Id);
    }

    [Fact]
    public async Task Rule_30_a_page_larger_than_fifty_is_refused()
    {
        var response = await Anonymous().GetAsync("/api/v1/properties?pageSize=51");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_30_the_page_is_twelve_listings_unless_asked_otherwise()
    {
        (await SearchAsync()).PageSize.ShouldBe(12);
    }

    [Fact]
    public async Task Rule_74_a_distance_without_a_landmark_is_refused()
    {
        (await Anonymous().GetAsync("/api/v1/properties?maxDistanceKm=5"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_74_sorting_by_distance_without_a_landmark_is_refused()
    {
        (await Anonymous().GetAsync("/api/v1/properties?sort=Distance"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rule_75_a_minimum_rent_above_the_maximum_is_refused()
    {
        (await Anonymous().GetAsync("/api/v1/properties?minRent=30000&maxRent=20000"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("cityId=999999")]
    [InlineData("areaId=999999")]
    [InlineData("landmarkId=999999")]
    [InlineData("facilityIds=999999")]
    public async Task Rule_85_a_filter_naming_nothing_is_refused(string filter)
    {
        var response = await Anonymous().GetAsync("/api/v1/properties?" + filter);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_rent_range_is_honoured()
    {
        var page = await SearchAsync("?minRent=15000&maxRent=25000&pageSize=50");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(card => card.MonthlyRent >= 15000 && card.MonthlyRent <= 25000);
    }

    [Fact]
    public async Task A_room_type_and_a_gender_policy_are_honoured()
    {
        var page = await SearchAsync("?roomType=Private&genderPolicy=Girls&pageSize=50");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(card => card.RoomType == "Private" && card.GenderPolicy == "Girls");
    }

    /// <summary>
    /// This test fills the listing it then expects the filter to remove.
    ///
    /// It used to take "the first card with no beds free" out of a page of fifty seeded
    /// listings, which stopped finding one as soon as enough tests had published their
    /// own — every fixture has two beds free and they sort to the top. Taking the last
    /// bed through the path the product actually uses (rules 21 and 25: the owner
    /// proposes, an admin approves) leaves the test owning its own subject.
    /// </summary>
    [Fact]
    public async Task Rule_78_available_only_removes_a_listing_with_no_bed_free()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "search-full");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);

        var revision = (await (await owner.Client.PatchJsonAsync(
                    $"/api/v1/owner/properties/{listing.Id}/availability", new { AvailableBeds = 0 }))
                .ReadAsync<OwnerListingUpdateDto>()).PendingRevision!;

        (await admin.Client.PostJsonAsync(
                $"/api/v1/admin/revisions/{revision.Id}/approve", new { }))
            .EnsureSuccessStatusCode();

        var everything = await SearchAsync($"?areaId={listing.AreaId}&pageSize=50");

        everything.Items.ShouldContain(card => card.Id == listing.Id);

        var available = await SearchAsync($"?areaId={listing.AreaId}&availableOnly=true&pageSize=50");

        available.Items.ShouldAllBe(card => card.AvailableBeds > 0);
        available.Items.ShouldNotContain(card => card.Id == listing.Id);
    }

    /// <summary>
    /// Rule 76 cannot be read off a card, which carries no facilities, so each result
    /// is opened to check that it really offers both.
    /// </summary>
    [Fact]
    public async Task Rule_76_two_facilities_mean_a_listing_that_offers_both()
    {
        var client = Anonymous();
        var facilities = await OwnerWorkspace.FacilitiesAsync(client);

        var wifi = facilities.First(f => f.Name == "Wi-Fi").Id;
        var laundry = facilities.First(f => f.Name == "Laundry").Id;

        var page = await SearchAsync($"?facilityIds={wifi},{laundry}&pageSize=50");
        var either = await SearchAsync($"?facilityIds={laundry}&pageSize=50");

        page.Items.ShouldNotBeEmpty();
        page.TotalCount.ShouldBeLessThanOrEqualTo(either.TotalCount);

        foreach (var card in page.Items)
        {
            var detail = await (await client.GetAsync($"/api/v1/properties/{card.Id}"))
                .ReadAsync<ListingDetailDto>();

            var ids = detail.Facilities.Select(f => f.Id).ToList();

            ids.ShouldContain(wifi);
            ids.ShouldContain(laundry);
        }
    }

    [Fact]
    public async Task Cheapest_first_means_cheapest_first()
    {
        var page = await SearchAsync("?sort=PriceLowToHigh&pageSize=50");

        page.Items.Select(c => c.MonthlyRent).ShouldBe(page.Items.Select(c => c.MonthlyRent).Order());
    }

    [Fact]
    public async Task Dearest_first_means_dearest_first()
    {
        var page = await SearchAsync("?sort=PriceHighToLow&pageSize=50");

        page.Items.Select(c => c.MonthlyRent)
            .ShouldBe(page.Items.Select(c => c.MonthlyRent).OrderDescending());
    }

    [Fact]
    public async Task The_default_order_is_the_most_recently_confirmed_first()
    {
        var page = await SearchAsync("?pageSize=50");

        page.Items.Select(c => c.AvailabilityConfirmedAt)
            .ShouldBe(page.Items.Select(c => c.AvailabilityConfirmedAt).OrderDescending());
    }

    /// <summary>
    /// Rule 31, and the one assertion in this suite that only passes if the formula
    /// really translated: the sort is done by SQL Server, and the number on the card
    /// is the same formula compiled in memory. They have to agree.
    /// </summary>
    [Fact]
    public async Task Rule_31_the_database_sorts_by_a_distance_the_card_agrees_with()
    {
        var landmarkId = await LandmarkIdAsync("Punjab");

        var page = await SearchAsync($"?landmarkId={landmarkId}&sort=Distance&pageSize=50");

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(card => card.DistanceKm != null);

        var distances = page.Items.Select(c => c.DistanceKm!.Value).ToList();

        distances.ShouldBe(distances.Order());
    }

    [Fact]
    public async Task Rule_80_a_distance_is_measured_from_the_landmark_and_rounded()
    {
        // This test publishes the listing it measures, rather than reaching into the
        // seed data for one.
        //
        // It used to take "the first Gulberg listing" out of a page of fifty, which
        // stopped being a seeded listing once enough tests had published their own — and
        // it had been passing anyway, because the fixture had been copied from the seeder
        // and stood on exactly the same coordinates. A test that owns its subject cannot
        // be wrong about which row it measured.
        var owner = await OwnerWorkspace.CreateAsync(_factory, "search-distance");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);

        var landmarkId = await LandmarkIdAsync("Punjab");

        var page = await SearchAsync($"?landmarkId={landmarkId}&areaId={listing.AreaId}&pageSize=50");

        var card = page.Items.First(c => c.Id == listing.Id);

        card.DistanceKm.ShouldBe(OwnerWorkspace.FixtureDistanceFromPunjabUniversityKm);
    }

    [Fact]
    public async Task Rule_80_no_landmark_means_no_distance()
    {
        (await SearchAsync("?pageSize=50")).Items.ShouldAllBe(card => card.DistanceKm == null);
    }

    [Fact]
    public async Task A_distance_filter_leaves_out_what_is_too_far()
    {
        var landmarkId = await LandmarkIdAsync("Punjab");

        var near = await SearchAsync($"?landmarkId={landmarkId}&maxDistanceKm=10&pageSize=50");
        var everywhere = await SearchAsync($"?landmarkId={landmarkId}&pageSize=50");

        near.Items.ShouldNotBeEmpty();
        near.Items.ShouldAllBe(card => card.DistanceKm <= 10);
        near.TotalCount.ShouldBeLessThan(everywhere.TotalCount);
    }

    /// <summary>
    /// Rule 77. One listing per page over the whole result set: any listing seen
    /// twice, or a page that comes back short in the middle, is an unstable order.
    /// </summary>
    [Fact]
    public async Task Rule_77_paging_one_at_a_time_shows_each_listing_exactly_once()
    {
        var first = await SearchAsync("?pageSize=1");
        var total = Math.Min(first.TotalCount, 12);

        var seen = new List<int>();

        for (var page = 1; page <= total; page++)
        {
            var one = await SearchAsync($"?pageSize=1&page={page}");

            one.Items.Count.ShouldBe(1, $"page {page} of {total} came back empty");
            seen.Add(one.Items[0].Id);
        }

        seen.Distinct().Count().ShouldBe(seen.Count);
    }

    [Fact]
    public async Task Rule_77_holds_when_the_order_is_one_that_ties()
    {
        // Rent ties across the seeded listings far more readily than a timestamp does.
        var first = await SearchAsync("?sort=PriceLowToHigh&pageSize=1");
        var total = Math.Min(first.TotalCount, 12);

        var seen = new List<int>();

        for (var page = 1; page <= total; page++)
        {
            var one = await SearchAsync($"?sort=PriceLowToHigh&pageSize=1&page={page}");
            seen.Add(one.Items.Single().Id);
        }

        seen.Distinct().Count().ShouldBe(seen.Count);
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_rather_than_an_error()
    {
        var page = await SearchAsync("?page=500");

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_card_carries_the_pin_the_map_view_needs()
    {
        var page = await SearchAsync("?pageSize=50");

        page.Items.ShouldAllBe(card => card.Latitude != 0 && card.Longitude != 0);
    }

    [Fact]
    public async Task A_card_carries_a_photograph_to_show()
    {
        var page = await SearchAsync("?pageSize=50");

        page.Items.ShouldAllBe(card => card.PrimaryThumbnailUrl != null && card.PhotoCount > 0);
    }
}
