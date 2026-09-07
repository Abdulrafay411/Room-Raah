using System.Net;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Enums;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class RegisterEndpointTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public RegisterEndpointTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"reg-{Guid.NewGuid():N}@roomraah.local";

    private static object Body(string email, string role = "Seeker", string password = "Password1") => new
    {
        FullName = "Integration Person",
        Email = email,
        PhoneNumber = "+923001112233",
        Password = password,
        Role = role,
        CnicNumber = role == "Owner" ? "3520112345671" : null
    };

    [Fact]
    public async Task Happy_path_creates_the_account_and_emails_a_code()
    {
        var email = NewEmail();

        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(email));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var user = await response.ReadAsync<UserSummaryDto>();
        user.Id.ShouldBeGreaterThan(0);
        user.Email.ShouldBe(email);
        user.Role.ShouldBe("Seeker");
        user.IsEmailVerified.ShouldBeFalse();

        _factory.Emails.LatestSecretFor(email, OtpPurpose.VerifyEmail).Length.ShouldBe(6);
    }

    [Fact]
    public async Task An_owner_registration_is_accepted_with_a_cnic()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(NewEmail(), "Owner"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadAsync<UserSummaryDto>()).Role.ShouldBe("Owner");
    }

    [Fact]
    public async Task Rule_1_a_duplicate_email_is_refused_with_422()
    {
        var email = NewEmail();
        (await _client.PostJsonAsync("/api/v1/auth/register", Body(email))).EnsureSuccessStatusCode();

        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(email));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorAsync()).Error.ShouldContain("already exists");
    }

    [Fact]
    public async Task Rule_1_an_email_is_unique_across_roles()
    {
        var email = NewEmail();
        (await _client.PostJsonAsync("/api/v1/auth/register", Body(email))).EnsureSuccessStatusCode();

        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(email, "Owner"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Rule_2_a_weak_password_is_refused_with_400_and_a_field_error()
    {
        var response = await _client.PostJsonAsync(
            "/api/v1/auth/register", Body(NewEmail(), password: "weak"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var error = await response.ReadErrorAsync();
        error.Errors.ShouldNotBeNull();
        error.Errors!.ShouldContain(e => e.Field == "Password");
    }

    [Fact]
    public async Task An_owner_without_a_cnic_is_refused_with_400()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", new
        {
            FullName = "No Cnic",
            Email = NewEmail(),
            PhoneNumber = "+923001112233",
            Password = "Password1",
            Role = "Owner"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "CnicNumber");
    }

    [Fact]
    public async Task An_admin_account_cannot_be_created_from_the_public_endpoint()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(NewEmail(), "Admin"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "Role");
    }

    [Fact]
    public async Task The_response_never_contains_the_password_or_the_phone_number()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/register", Body(NewEmail()));

        var body = await response.Content.ReadAsStringAsync();

        body.ShouldNotContain("Password1");
        body.ShouldNotContain("+923001112233");
        body.ShouldNotContain("passwordHash");
    }
}
