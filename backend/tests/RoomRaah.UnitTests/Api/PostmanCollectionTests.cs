using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Shouldly;

namespace RoomRaah.UnitTests.Api;

/// <summary>
/// The Deliverables list asks for "a Postman collection covering every endpoint", and a
/// hand-written collection drifts from the controllers the moment somebody adds one.
///
/// This reads both and compares them, so the claim is checked rather than made. It is
/// deliberately a two-way comparison: a collection missing an endpoint is an incomplete
/// deliverable, and a collection naming an endpoint that no longer exists is a request
/// that 404s in front of whoever opened it.
/// </summary>
public class PostmanCollectionTests
{
    private const string CollectionFileName = "RoomRaah.postman_collection.json";

    /// <summary>
    /// Route templates and collection URLs differ where a parameter goes — `{id:int}`
    /// against `{{propertyId}}` — so both are reduced to the same shape before they are
    /// compared. Everything else about the path has to match exactly.
    /// </summary>
    private static string Normalise(string path)
    {
        path = Regex.Replace(path, @"\{\{[^}]+\}\}", "*");
        path = Regex.Replace(path, @"\{[^}]+\}", "*");

        return path.Trim('/').ToLowerInvariant();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RoomRaah.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull("The solution root was not found above the test output directory.");

        return directory!.FullName;
    }

    /// <summary>Every action on every controller, as a method and a normalised path.</summary>
    private static HashSet<string> Endpoints()
    {
        var found = new HashSet<string>();

        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var controller in controllers)
        {
            var route = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var tail = verb.Template;

                    var path = string.IsNullOrEmpty(tail) ? route : $"{route}/{tail}";

                    foreach (var method in verb.HttpMethods)
                    {
                        found.Add($"{method.ToUpperInvariant()} {Normalise(path)}");
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Every request in the collection, in the same shape.</summary>
    private static HashSet<string> Requests()
    {
        var path = Path.Combine(RepositoryRoot(), CollectionFileName);

        File.Exists(path).ShouldBeTrue($"{CollectionFileName} is missing from the repository root.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var found = new HashSet<string>();

        Walk(document.RootElement.GetProperty("item"), found);

        return found;
    }

    private static void Walk(JsonElement items, HashSet<string> found)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("item", out var nested))
            {
                Walk(nested, found);
                continue;
            }

            var request = item.GetProperty("request");

            var method = request.GetProperty("method").GetString()!;
            var raw = request.GetProperty("url").GetProperty("raw").GetString()!;

            // The query string is not part of an endpoint's identity.
            var withoutQuery = raw.Split('?')[0].Replace("{{baseUrl}}", "api/v1");

            found.Add($"{method.ToUpperInvariant()} {Normalise(withoutQuery)}");
        }
    }

    [Fact]
    public void The_collection_covers_every_endpoint_in_the_product()
    {
        var missing = Endpoints().Except(Requests()).OrderBy(x => x).ToList();

        missing.ShouldBeEmpty(
            "The Postman collection does not cover: " + string.Join(", ", missing));
    }

    /// <summary>
    /// The other direction. A request for an endpoint that no longer exists is a 404 in
    /// front of whoever opened the collection to see what the product does.
    /// </summary>
    [Fact]
    public void The_collection_names_no_endpoint_the_product_does_not_have()
    {
        var stale = Requests().Except(Endpoints()).OrderBy(x => x).ToList();

        stale.ShouldBeEmpty(
            "The Postman collection names endpoints that do not exist: " + string.Join(", ", stale));
    }

    /// <summary>
    /// The collection authenticates as a whole and turns it off only where a caller
    /// cannot have a token yet. A request that forgot the exception is one nobody can
    /// run before signing in — which is every request in the collection, on a fresh
    /// install.
    /// </summary>
    [Fact]
    public void The_public_auth_requests_are_the_ones_that_send_no_token()
    {
        var path = Path.Combine(RepositoryRoot(), CollectionFileName);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        document.RootElement.GetProperty("auth").GetProperty("type").GetString().ShouldBe("bearer");

        var anonymous = new List<string>();

        CollectAnonymous(document.RootElement.GetProperty("item"), anonymous);

        anonymous.ShouldNotBeEmpty();
        anonymous.ShouldContain(p => p.Contains("auth/login"));
        anonymous.ShouldContain(p => p.Contains("auth/register"));

        // Nothing outside the anonymous areas may opt out of the bearer token.
        anonymous.ShouldAllBe(p =>
            p.Contains("auth/") || p.Contains("properties") || p.Contains("locations") ||
            p.Contains("facilities"));
    }

    private static void CollectAnonymous(JsonElement items, List<string> anonymous)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("item", out var nested))
            {
                CollectAnonymous(nested, anonymous);
                continue;
            }

            var request = item.GetProperty("request");

            if (request.TryGetProperty("auth", out var auth) &&
                auth.GetProperty("type").GetString() == "noauth")
            {
                anonymous.Add(request.GetProperty("url").GetProperty("raw").GetString()!);
            }
        }
    }
}
