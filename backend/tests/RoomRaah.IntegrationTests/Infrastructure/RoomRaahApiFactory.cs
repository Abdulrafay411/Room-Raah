using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RoomRaah.Application.Interfaces;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a real SQL Server and a real Redis, each in a container
/// created for this run and thrown away afterwards. Only SMTP is substituted.
/// </summary>
public class RoomRaahApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine")
        .Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

    public CapturingEmailSender Emails { get; } = new();

    public CapturingLoggerProvider Logs { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _redis.StartAsync(), _azurite.StartAsync());
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(
            _sqlServer.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _azurite.DisposeAsync().AsTask());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development runs the migration and the seeder on startup, which is exactly
        // the path the deliverables promise: a schema built from empty.
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _sqlServer.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("ConnectionStrings:AzureBlob", _azurite.GetConnectionString());

        // The clocks are off; the work is not. Rule 127 puts every line of a background
        // job in a MediatR handler, so the tests send the command and get a real answer
        // against a real database. A timer firing mid-run would send emails no test
        // asked for and make every email count in the suite depend on how long the run
        // happened to take.
        builder.UseSetting("BackgroundJobs:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<RoomRaahApiFactory>
{
    public const string Name = "RoomRaah API";
}
