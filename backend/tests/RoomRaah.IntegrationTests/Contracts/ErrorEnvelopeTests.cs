using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Contracts;

/// <summary>
/// Every failure in this API answers in one envelope, whatever produced it. A
/// request rejected by model binding never reaches the exception middleware, so
/// without the response factory in Program.cs it would answer in ProblemDetails
/// instead — same endpoint, two shapes, and a client that renders one of them.
/// </summary>
[Collection(ApiCollection.Name)]
public class ErrorEnvelopeTests
{
    private readonly HttpClient _client;

    public ErrorEnvelopeTests(RoomRaahApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static async Task AssertEnvelopeAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        root.GetProperty("success").GetBoolean().ShouldBeFalse();
        root.GetProperty("statusCode").GetInt32().ShouldBe(400);
        root.TryGetProperty("error", out _).ShouldBeTrue();

        // A list of field errors, never a dictionary keyed by field.
        var errors = root.GetProperty("errors");
        errors.ValueKind.ShouldBe(JsonValueKind.Array);
        errors.EnumerateArray().ShouldNotBeEmpty();

        foreach (var error in errors.EnumerateArray())
        {
            error.GetProperty("field").GetString().ShouldNotBeNullOrWhiteSpace();
            error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task A_rule_broken_in_the_validation_pipeline_uses_the_envelope()
    {
        await AssertEnvelopeAsync(await _client.GetAsync("/api/v1/locations/suggest?q=n"));
    }

    [Fact]
    public async Task A_missing_query_parameter_uses_the_envelope()
    {
        await AssertEnvelopeAsync(await _client.GetAsync("/api/v1/locations/suggest"));
    }

    [Fact]
    public async Task An_empty_query_parameter_uses_the_envelope()
    {
        await AssertEnvelopeAsync(await _client.GetAsync("/api/v1/locations/suggest?q="));
    }

    [Fact]
    public async Task An_empty_query_parameter_reports_the_field_the_caller_actually_sent()
    {
        var response = await _client.GetAsync("/api/v1/locations/suggest?q=");

        (await response.ReadErrorAsync()).Errors!.ShouldContain(e => e.Field == "q");
    }

    [Fact]
    public async Task A_body_that_model_binding_cannot_read_uses_the_envelope()
    {
        // Email arrives as an object where a string belongs: rejected during binding,
        // which is the path that used to answer in a different shape.
        var content = new StringContent(
            """{"email":{"not":"a string"},"password":"Password1"}""",
            Encoding.UTF8,
            "application/json");

        await AssertEnvelopeAsync(await _client.PostAsync("/api/v1/auth/login", content));
    }

    [Fact]
    public async Task A_route_value_of_the_wrong_type_is_not_routed_at_all()
    {
        // The :int constraint means this never matches the action, so it is a 404
        // rather than a malformed request.
        var response = await _client.GetAsync("/api/v1/locations/cities/abc/areas");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
