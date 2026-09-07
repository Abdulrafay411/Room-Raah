using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Inspections;

/// <summary>
/// The `Admin — users` area, which is in §5 and in the twenty-page structure and
/// belonged to no phase of the build plan at all.
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminUserEndpointTests
{
    private readonly RoomRaahApiFactory _factory;

    public AdminUserEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> FindAsync(AdminWorkspace admin, string email)
    {
        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        return page.Items.Single(u => u.Email == email).Id;
    }

    // -------------------------------------------------------------- the list

    [Fact]
    public async Task An_admin_finds_an_account_by_its_address()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-find");

        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(seeker.Email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        var row = page.Items.Single();

        row.Email.ShouldBe(seeker.Email);
        row.Role.ShouldBe("Seeker");
        row.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// Rule 117, asserted on the raw body. The list is where a leak would be worst —
    /// fifty accounts is fifty identity numbers on one screen — so it is asserted on
    /// what actually comes back rather than on the shape of the type.
    /// </summary>
    [Fact]
    public async Task The_list_carries_no_identity_number_and_no_phone_number()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var owner = await OwnerWorkspace.CreateAsync(_factory, "adminusers-privacy");

        var body = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(owner.Email)}"))
            .Content.ReadAsStringAsync();

        body.ShouldContain(owner.Email);
        body.ShouldNotContain("3520112345671");
        body.ShouldNotContain("+92300");
        body.ShouldNotContain("cnic", Case.Insensitive);
    }

    /// <summary>
    /// Rule 117, and the half of it a probe found nothing asserting.
    ///
    /// A deactivated account is *the* account an admin comes to this list looking for —
    /// the whole reason the query ignores the soft-delete filter. Every other test here
    /// looked up accounts that were still active, so removing that IgnoreQueryFilters
    /// broke nothing and the suite stayed green.
    /// </summary>
    [Fact]
    public async Task A_deactivated_account_is_still_on_the_list()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-listed-off");

        var id = await FindAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/deactivate", new { Reason = "Repeated abusive messages." }))
            .EnsureSuccessStatusCode();

        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(seeker.Email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        var row = page.Items.Single(u => u.Id == id);

        row.IsActive.ShouldBeFalse();
    }

    /// <summary>
    /// And the filter that exists to ask for exactly those. An empty answer here would
    /// mean the switched-off accounts are unreachable from the one screen built to
    /// manage them.
    /// </summary>
    [Fact]
    public async Task The_list_can_be_narrowed_to_the_accounts_that_are_switched_off()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-filter-off");

        var id = await FindAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/deactivate", new { Reason = "Repeated abusive messages." }))
            .EnsureSuccessStatusCode();

        var off = await (await admin.Client.GetAsync("/api/v1/admin/users?isActive=false&pageSize=50"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        off.Items.ShouldNotBeEmpty();
        off.Items.ShouldAllBe(u => !u.IsActive);
        off.Items.Select(u => u.Id).ShouldContain(id);

        var on = await (await admin.Client.GetAsync("/api/v1/admin/users?isActive=true&pageSize=50"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        on.Items.Select(u => u.Id).ShouldNotContain(id);
    }

    [Fact]
    public async Task The_list_can_be_narrowed_to_one_role()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        var page = await (await admin.Client.GetAsync("/api/v1/admin/users?role=Owner&pageSize=50"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(u => u.Role == "Owner");
    }

    /// <summary>
    /// A role that is a number is the case <c>Enum.TryParse</c> accepts on its own, and
    /// it is a 400 rather than an empty list — a filter nobody can satisfy is a client
    /// bug worth saying out loud.
    /// </summary>
    [Theory]
    [InlineData("Moderator")]
    [InlineData("3")]
    public async Task A_role_the_product_does_not_have_is_refused(string role)
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        (await admin.Client.GetAsync($"/api/v1/admin/users?role={role}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------- the detail

    /// <summary>
    /// Rule 116, the single exception to rule 33 in the product: an admin recording an
    /// identity check has to be able to see the identity.
    /// </summary>
    [Fact]
    public async Task The_detail_carries_the_identity_the_check_is_about()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var owner = await OwnerWorkspace.CreateAsync(_factory, "adminusers-detail");

        var id = await FindAsync(admin, owner.Email);

        var dto = await (await admin.Client.GetAsync($"/api/v1/admin/users/{id}"))
            .ReadAsync<AdminUserDto>();

        dto.CnicNumber.ShouldBe("3520112345671");
        dto.PhoneNumber.ShouldNotBeNullOrWhiteSpace();
        dto.IdentityStatus.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Only an owner has one, so only an owner's detail carries one.</summary>
    [Fact]
    public async Task A_seeker_has_no_identity_number_to_show()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-seeker");

        var id = await FindAsync(admin, seeker.Email);

        var dto = await (await admin.Client.GetAsync($"/api/v1/admin/users/{id}"))
            .ReadAsync<AdminUserDto>();

        dto.CnicNumber.ShouldBeNull();
    }

    [Fact]
    public async Task An_account_that_does_not_exist_is_a_404()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        (await admin.Client.GetAsync("/api/v1/admin/users/987654"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --------------------------------------------------- deactivate and back

    [Fact]
    public async Task An_account_is_switched_off_and_can_no_longer_sign_in()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-off");

        var id = await FindAsync(admin, seeker.Email);

        var deactivated = await (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/deactivate",
                new { Reason = "Repeated abusive messages." }))
            .ReadAsync<AdminUserSummaryDto>();

        deactivated.IsActive.ShouldBeFalse();

        // Rule 11, which is what deactivating is for.
        var login = await _factory.CreateClient().PostJsonAsync("/api/v1/auth/login", new
        {
            Email = seeker.Email,
            Password = "Password1"
        });

        login.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// §5 listed only the deactivation, which made an admin's misclick permanent and
    /// left an account no route back even after whatever caused it was resolved.
    /// </summary>
    [Fact]
    public async Task An_account_can_be_switched_back_on()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-back");

        var id = await FindAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/deactivate", new { Reason = "A mistake." }))
            .EnsureSuccessStatusCode();

        var restored = await (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/reactivate",
                new { Reason = "The report against this account was dismissed." }))
            .ReadAsync<AdminUserSummaryDto>();

        restored.IsActive.ShouldBeTrue();

        (await _factory.CreateClient().PostJsonAsync("/api/v1/auth/login", new
        {
            Email = seeker.Email,
            Password = "Password1"
        })).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Rule 120. The query filters already take a deactivated owner's listings out of
    /// every public list, and they come back when the account does — which is the whole
    /// reason nothing is written onto the listings themselves.
    /// </summary>
    [Fact]
    public async Task Deactivating_an_owner_takes_their_listings_off_the_site_and_back()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var owner = await OwnerWorkspace.CreateAsync(_factory, "adminusers-listings");
        var listing = await owner.CreatePublishedAsync(admin);

        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}")).EnsureSuccessStatusCode();

        var id = await FindAsync(admin, owner.Email);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/deactivate",
                new { Reason = "Listings do not match the properties." }))
            .EnsureSuccessStatusCode();

        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/reactivate", new { Reason = "Resolved." }))
            .EnsureSuccessStatusCode();

        (await anonymous.GetAsync($"/api/v1/properties/{listing.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Switching_an_account_to_the_state_it_is_already_in_is_refused()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-again");

        var id = await FindAsync(admin, seeker.Email);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{id}/reactivate", new { Reason = "Already on." }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>Rule 118, the protection that stops the admin panel emptying itself.</summary>
    [Fact]
    public async Task An_admin_cannot_switch_off_another_admin()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(AdminWorkspace.NonInspectorEmail)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        var otherAdmin = page.Items.Single(u => u.Email == AdminWorkspace.NonInspectorEmail).Id;

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{otherAdmin}/deactivate", new { Reason = "Because." }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_admin_cannot_switch_off_their_own_account()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);

        var page = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(AdminWorkspace.Email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        var self = page.Items.Single(u => u.Email == AdminWorkspace.Email).Id;

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/users/{self}/deactivate", new { Reason = "Because." }))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_change_with_no_reason_is_refused()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-noreason");

        var id = await FindAsync(admin, seeker.Email);

        var response = await admin.Client.PatchJsonAsync(
            $"/api/v1/admin/users/{id}/deactivate", new { Reason = "  " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull();
    }

    // ------------------------------------------------------------------ auth

    [Fact]
    public async Task Nobody_but_an_admin_reaches_this_area()
    {
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "adminusers-forbidden");

        (await seeker.Client.GetAsync("/api/v1/admin/users"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await _factory.CreateClient().GetAsync("/api/v1/admin/users"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
