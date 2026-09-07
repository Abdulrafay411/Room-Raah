using RoomRaah.Application.DTOs;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Contracts;

/// <summary>
/// §Testing Checklist, privacy: *no API response anywhere — search, detail, reviews,
/// messages, visit requests — contains an owner's phone number, WhatsApp number or email
/// address.*
///
/// The DTO sweep in the unit tests checks the shapes; this checks the bodies. They catch
/// different things. A shape can be right while a handler serialises something extra, and
/// a body can be clean today because no test happened to populate the field that leaks.
/// So this walks one real journey with real data in it and reads what actually comes back.
///
/// It is deliberately one test over many endpoints rather than an assertion bolted onto
/// each area's own suite: rule 33 is a property of the product, and a per-area version of
/// it is a rule with one chance per area of being forgotten.
///
/// Writing it found that rule 33's own wording was wrong. The rule named one exception,
/// the admin user *detail*; the admin user *list* returns an email address too, and has
/// to — a list of accounts that cannot tell two people with the same name apart is not a
/// list anybody can act on. The rule now names the area and says which of the three
/// fields each endpoint carries.
/// </summary>
[Collection(ApiCollection.Name)]
public class PrivacySweepTests
{
    private readonly RoomRaahApiFactory _factory;

    public PrivacySweepTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The prefix every seeded and fixture account registers with. Asserting on the
    /// literal digits catches a leak that a property-name check would miss entirely —
    /// a phone number serialised under a field called anything at all.
    /// </summary>
    private const string PhonePrefix = "+92300";

    private const string OwnerCnic = "3520112345671";

    [Fact]
    public async Task No_response_in_the_product_carries_an_owner_s_contact_details()
    {
        var owner = await OwnerWorkspace.CreateAsync(_factory, "privacy-sweep");
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var listing = await owner.CreatePublishedAsync(admin);
        var seeker = await SeekerWorkspace.CreateAsync(_factory, "privacy-sweep");
        var anonymous = _factory.CreateClient();

        // Real activity, so the responses below have something in them to leak. A sweep
        // over empty collections proves nothing.
        var visit = await seeker.RequestVisitAsync(listing.Id);

        (await owner.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/respond", new
        {
            Status = "Accepted",
            OwnerResponseNote = "Come after 4pm."
        })).EnsureSuccessStatusCode();

        (await seeker.Client.PostJsonAsync($"/api/v1/saved/{listing.Id}", new { }))
            .EnsureSuccessStatusCode();

        var conversation = await (await seeker.Client.PostJsonAsync(
                "/api/v1/conversations", new { PropertyId = listing.Id }))
            .ReadAsync<ConversationDto>();

        (await seeker.Client.PostJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages",
                new { Body = "Is the room still free from the 1st?" }))
            .EnsureSuccessStatusCode();

        (await owner.Client.PostJsonAsync(
                $"/api/v1/conversations/{conversation.Id}/messages",
                new { Body = "Yes, one bed is free." }))
            .EnsureSuccessStatusCode();

        await SeekerWorkspace.BackdateAsync(_factory, visit.Id);

        (await seeker.Client.PatchJsonAsync($"/api/v1/visits/{visit.Id}/complete", new { }))
            .EnsureSuccessStatusCode();

        var review = await seeker.ReviewAsync(listing.Id);

        (await admin.Client.PatchJsonAsync(
                $"/api/v1/admin/reviews/{review.Id}", new { State = "Approved" }))
            .EnsureSuccessStatusCode();

        // Everything a person can read, from every side of the product.
        var responses = new List<(string Where, string Body)>
        {
            ("search", await GetAsync(anonymous, "/api/v1/properties?pageSize=50")),
            ("featured", await GetAsync(anonymous, "/api/v1/properties/featured")),
            ("compare", await GetAsync(anonymous, $"/api/v1/properties/compare?ids={listing.Id}")),
            ("public detail", await GetAsync(anonymous, $"/api/v1/properties/{listing.Id}")),
            ("public photos", await GetAsync(anonymous, $"/api/v1/properties/{listing.Id}/photos")),
            ("public reviews", await GetAsync(anonymous, $"/api/v1/properties/{listing.Id}/reviews")),

            ("shortlist", await GetAsync(seeker.Client, "/api/v1/saved")),
            ("seeker visits", await GetAsync(seeker.Client, "/api/v1/visits/my")),
            ("seeker inbox", await GetAsync(seeker.Client, "/api/v1/conversations")),
            ("seeker thread", await GetAsync(seeker.Client,
                $"/api/v1/conversations/{conversation.Id}/messages")),

            ("owner visits", await GetAsync(owner.Client, "/api/v1/owner/visits")),
            ("owner inbox", await GetAsync(owner.Client, "/api/v1/conversations")),
            ("owner listings", await GetAsync(owner.Client, "/api/v1/owner/properties")),

            ("admin listing detail", await GetAsync(admin.Client,
                $"/api/v1/admin/properties/{listing.Id}")),
            ("admin review queue", await GetAsync(admin.Client, "/api/v1/admin/reviews?state=Approved")),
            ("admin report queue", await GetAsync(admin.Client, "/api/v1/admin/reports")),
            ("admin inspection worklist", await GetAsync(admin.Client, "/api/v1/admin/inspections/due")),
            ("admin user list", await GetAsync(admin.Client, "/api/v1/admin/users?pageSize=50")),
        };

        foreach (var (where, body) in responses)
        {
            // The admin user area is the exception rule 33 names, and the shape of the
            // exception is not the same on both of its endpoints: the list carries an
            // email address because that is what tells two accounts with the same name
            // apart, and only the detail adds the phone number and the CNIC. So the email
            // assertion skips the list — and the two below do not, which is the half of
            // the exception that stays narrow.
            if (where != "admin user list")
            {
                body.ShouldNotContain(owner.Email, Case.Insensitive,
                    $"The {where} response carries the owner's email address.");
            }

            body.ShouldNotContain(PhonePrefix, Case.Sensitive,
                $"The {where} response carries a phone number.");

            body.ShouldNotContain(OwnerCnic, Case.Sensitive,
                $"The {where} response carries a CNIC.");

            body.ShouldNotContain("whatsapp", Case.Insensitive,
                $"The {where} response mentions WhatsApp, which this product does not have.");
        }
    }

    /// <summary>
    /// The one exception, asserted as an exception.
    ///
    /// Rule 116 exists because an admin recording the OwnerIdentity check was being asked
    /// to confirm an identity without being shown it. Leaving this endpoint out of the
    /// sweep above would hide it; asserting it here says out loud that the product has
    /// exactly one place where those fields appear, and that it is this one.
    /// </summary>
    [Fact]
    public async Task The_admin_user_detail_is_the_only_place_those_fields_appear()
    {
        var admin = await AdminWorkspace.CreateAsync(_factory);
        var owner = await OwnerWorkspace.CreateAsync(_factory, "privacy-exception");

        var list = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(owner.Email)}"))
            .ReadAsync<PagedResult<AdminUserSummaryDto>>();

        var id = list.Items.Single(u => u.Email == owner.Email).Id;

        // The list, which is not the exception.
        var listBody = await (await admin.Client.GetAsync(
                $"/api/v1/admin/users?q={Uri.EscapeDataString(owner.Email)}"))
            .Content.ReadAsStringAsync();

        listBody.ShouldNotContain(PhonePrefix);
        listBody.ShouldNotContain(OwnerCnic);

        // The detail, which is.
        var detailBody = await (await admin.Client.GetAsync($"/api/v1/admin/users/{id}"))
            .Content.ReadAsStringAsync();

        detailBody.ShouldContain(PhonePrefix);
        detailBody.ShouldContain(OwnerCnic);
    }

    private static async Task<string> GetAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
