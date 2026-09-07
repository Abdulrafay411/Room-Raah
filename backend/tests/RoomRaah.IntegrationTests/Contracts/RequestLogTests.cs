using System.Net;
using Microsoft.Extensions.Logging;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Contracts;

/// <summary>
/// The access log has to agree with what the client was sent. It did not: request
/// logging ran inside the exception middleware, so a failure on its way to becoming
/// a 404 was still an exception when the logger saw it, and every handled 4xx was
/// written as "responded 500" at Error level.
/// </summary>
[Collection(ApiCollection.Name)]
public class RequestLogTests
{
    private readonly RoomRaahApiFactory _factory;
    private readonly HttpClient _client;

    public RequestLogTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private CapturingLoggerProvider.Entry Latest(string path)
    {
        var entries = _factory.Logs.RequestsFor(path);

        entries.ShouldNotBeEmpty($"Nothing was logged for {path}.");

        return entries[^1];
    }

    [Fact]
    public async Task A_successful_request_is_logged_with_its_real_status()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Latest("/api/v1/locations/cities").Message.ShouldContain("responded 200");
    }

    [Fact]
    public async Task A_not_found_is_logged_as_a_not_found_and_not_as_a_server_error()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities/987654/areas");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var logged = Latest("/api/v1/locations/cities/987654/areas");

        logged.Message.ShouldContain("responded 404");
        logged.Message.ShouldNotContain("responded 500");
        logged.Level.ShouldNotBe(LogLevel.Error);
    }

    [Fact]
    public async Task A_bad_request_is_logged_as_a_bad_request()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities/0/areas");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var logged = Latest("/api/v1/locations/cities/0/areas");

        logged.Message.ShouldContain("responded 400");
        logged.Level.ShouldNotBe(LogLevel.Error);
    }

    [Fact]
    public async Task A_rejected_sign_in_is_logged_as_a_401_not_a_server_error()
    {
        var response = await _client.PostJsonAsync("/api/v1/auth/login", new
        {
            Email = "nobody@roomraah.local",
            Password = "Password1"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var logged = Latest("/api/v1/auth/login");

        logged.Message.ShouldContain("responded 401");
        logged.Level.ShouldNotBe(LogLevel.Error);
    }

    [Fact]
    public async Task A_handled_failure_leaves_nothing_at_error_level()
    {
        // 987654 names no city, so the whole trail of this request is findable by it
        // while every other suite is writing to the same log.
        var response = await _client.GetAsync("/api/v1/locations/cities/987654/areas");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var trail = _factory.Logs.Mentioning("987654");

        trail.ShouldNotBeEmpty();
        trail.ShouldAllBe(e => e.Level != LogLevel.Error);
        trail.ShouldContain(e => e.Message.Contains("refused with 404"));
        trail.ShouldNotContain(e => e.Message.Contains("Unhandled"));
    }

    [Fact]
    public async Task A_refusal_is_logged_with_the_reason_as_well_as_the_status()
    {
        var response = await _client.GetAsync("/api/v1/locations/cities/987654/areas");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var refusal = _factory.Logs
            .Mentioning("987654")
            .Last(e => e.Message.StartsWith("Request "));

        refusal.Level.ShouldBe(LogLevel.Information);
        refusal.Message.ShouldContain("GET");
        refusal.Message.ShouldContain("refused with 404");
        refusal.Message.ShouldContain("987654");
    }

    [Fact]
    public async Task The_request_log_uses_the_documented_template()
    {
        await _client.GetAsync("/api/v1/facilities");

        var logged = Latest("/api/v1/facilities");

        // Asserted structurally: the method and path are rendered with quotes through
        // the provider bridge and without them in the file sink, and neither is the
        // point. What matters is that all five parts of the template are present.
        logged.Message.ShouldStartWith("HTTP ");
        logged.Message.ShouldContain("GET");
        logged.Message.ShouldContain("/api/v1/facilities");
        logged.Message.ShouldContain("responded 200 in ");
        logged.Message.ShouldEndWith("ms");
    }
}
