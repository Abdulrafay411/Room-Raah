using System.Net;
using System.Text.Json;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Contracts;

/// <summary>
/// §Deliverables: *Swagger UI at `/swagger` with the JWT scheme registered and every
/// endpoint documented.*
///
/// Held to the same standard as the Postman collection: a deliverable nothing checks is
/// a deliverable somebody is trusting. A controller added without XML comments, or a
/// security scheme that stopped being registered, both leave Swagger technically present
/// and useless — and both are silent.
/// </summary>
[Collection(ApiCollection.Name)]
public class SwaggerContractTests
{
    private readonly RoomRaahApiFactory _factory;

    public SwaggerContractTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<JsonDocument> DocumentAsync()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_document_is_served_and_describes_every_endpoint()
    {
        using var document = await DocumentAsync();

        var paths = document.RootElement.GetProperty("paths");

        var operations = paths.EnumerateObject()
            .Sum(p => p.Value.EnumerateObject().Count(o => IsVerb(o.Name)));

        // The same 68 the Postman collection covers and the controllers declare.
        operations.ShouldBe(68);
    }

    /// <summary>
    /// Without this a reader can see every endpoint and call none of them: the UI has no
    /// Authorize button, so every protected route answers 401 and looks broken.
    /// </summary>
    [Fact]
    public async Task The_jwt_scheme_is_registered()
    {
        using var document = await DocumentAsync();

        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        schemes.EnumerateObject().ShouldNotBeEmpty();

        var bearer = schemes.EnumerateObject().First().Value;

        // `type: http` with `scheme: bearer` is how OpenAPI 3 describes a bearer token,
        // and it carries no `in` — that belongs to `type: apiKey`, which this is not.
        bearer.GetProperty("type").GetString().ShouldBe("http", StringCompareShould.IgnoreCase);
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer", StringCompareShould.IgnoreCase);
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");

        // And it is required, or the Authorize button unlocks nothing.
        document.RootElement.GetProperty("security").EnumerateArray().ShouldNotBeEmpty();
    }

    /// <summary>
    /// The XML comments are the documentation half of the deliverable. A handful of
    /// summaries would satisfy a "does it have any" check, so this asks that nearly all
    /// of them do — a new controller written without them fails here rather than quietly
    /// shipping a blank page.
    /// </summary>
    [Fact]
    public async Task Nearly_every_operation_carries_a_summary()
    {
        using var document = await DocumentAsync();

        var undocumented = new List<string>();
        var total = 0;

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject().Where(o => IsVerb(o.Name)))
            {
                total++;

                if (!operation.Value.TryGetProperty("summary", out var summary) ||
                    string.IsNullOrWhiteSpace(summary.GetString()))
                {
                    undocumented.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        total.ShouldBeGreaterThan(0);

        undocumented.ShouldBeEmpty(
            "These operations have no summary in the OpenAPI document: "
            + string.Join(", ", undocumented));
    }

    private static bool IsVerb(string name) =>
        name is "get" or "post" or "put" or "patch" or "delete";
}
