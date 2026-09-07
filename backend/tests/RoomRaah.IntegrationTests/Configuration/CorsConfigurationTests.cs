using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RoomRaah.IntegrationTests.Infrastructure;
using Shouldly;

namespace RoomRaah.IntegrationTests.Configuration;

/// <summary>
/// CORS is configuration rather than a constant, and the reason is a deployment: the
/// frontend is served from an origin this repository cannot know, and an API that only
/// ever trusts localhost is an API that refuses the only client it has.
///
/// The origins are named rather than wildcarded, and that is not taste — SignalR's
/// negotiate step sends credentials, and a wildcard origin is not allowed with them.
/// A deployment that reached for "*" would find messaging silently broken.
/// </summary>
public class CorsConfigurationTests
{
    private static WebApplicationFactory<Program> Factory(params string[] origins)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Not Development, so startup does not migrate and seed. This asks a
            // question about the CORS policy the host builds, and answering it does
            // not need a database — booting one would make a configuration test
            // depend on a container.
            builder.UseEnvironment("Staging");

            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=unused;Database=RoomRaahCorsProbe;Trusted_Connection=False");

            for (var i = 0; i < origins.Length; i++)
            {
                builder.UseSetting($"Cors:AllowedOrigins:{i}", origins[i]);
            }
        });
    }

    private static async Task<HttpResponseMessage> PreflightAsync(
        WebApplicationFactory<Program> factory, string origin)
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/facilities");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task A_configured_origin_is_allowed()
    {
        using var factory = Factory("https://roomraah.example");

        var response = await PreflightAsync(factory, "https://roomraah.example");

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed).ShouldBeTrue();
        allowed!.ShouldContain("https://roomraah.example");
    }

    [Fact]
    public async Task An_origin_nobody_configured_is_not()
    {
        using var factory = Factory("https://roomraah.example");

        var response = await PreflightAsync(factory, "https://somebody-else.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    /// <summary>
    /// Credentials are what SignalR's negotiate step sends, and what a wildcard origin
    /// would make illegal. If this header ever stops coming back, live messaging stops
    /// working from the browser and nothing else does — which is a hard failure to
    /// trace from the symptom.
    /// </summary>
    [Fact]
    public async Task Credentials_are_allowed_so_the_hub_can_negotiate()
    {
        using var factory = Factory("https://roomraah.example");

        var response = await PreflightAsync(factory, "https://roomraah.example");

        response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentials)
            .ShouldBeTrue();
        credentials!.ShouldContain("true");
    }

    /// <summary>
    /// With nothing configured the Angular dev server still works, so a fresh checkout
    /// needs no configuration to be usable.
    /// </summary>
    [Fact]
    public async Task With_nothing_configured_the_dev_server_is_still_allowed()
    {
        using var factory = Factory();

        var response = await PreflightAsync(factory, "http://localhost:4200");

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed).ShouldBeTrue();
        allowed!.ShouldContain("http://localhost:4200");
    }
}
