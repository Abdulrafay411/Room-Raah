using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Shouldly;
using Testcontainers.Azurite;
using Testcontainers.MsSql;
using Testcontainers.Redis;

namespace RoomRaah.IntegrationTests.Configuration;

/// <summary>
/// Whether startup creates the schema, and whether it fills it, are two separate
/// decisions that are now two separate switches.
///
/// They were one, and the one was the hosting environment. The first real deployment
/// of this API ran as Production, so neither happened: the process came up, bound its
/// port, reported itself started, and then failed every single request against a
/// database that had never been created. Nothing in the startup log said so — the
/// first sign was a 500 on a listings page.
///
/// These tests hold the two switches apart, because the combination a real deployment
/// wants is the one that did not exist before: migrate, and do not seed. The seeders
/// write demo accounts whose password is printed in a document handed to a frontend
/// developer, and no live site should ever be one environment variable away from
/// having them.
/// </summary>
public class StartupDatabaseTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // The seeding test needs both of these, and that is worth saying rather than
    // hiding: filling the database reaches the cache and the photo store on its way
    // through. A test that asserted seeding works without them would be asserting it
    // about a startup path no deployment ever takes.
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly AzuriteContainer _azurite =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlServer.StartAsync(), _redis.StartAsync(), _azurite.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await _sqlServer.DisposeAsync();
        await _redis.DisposeAsync();
        await _azurite.DisposeAsync();
    }

    /// <summary>
    /// Each test gets a database of its own on the shared server, so what one of them
    /// creates is invisible to the one that asserts nothing was created.
    /// </summary>
    private string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = database
        }.ConnectionString;

    private async Task<bool> DatabaseExistsAsync(string database)
    {
        await using var connection = new SqlConnection(_sqlServer.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        command.Parameters.AddWithValue("@name", database);

        return (int)(await command.ExecuteScalarAsync())! > 0;
    }

    private async Task<int> CountAsync(string database, string table)
    {
        await using var connection = new SqlConnection(ConnectionStringFor(database));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM [{table}]";

        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Deliberately not Development, because Development is the thing that used to
    /// decide this and the point is that it no longer does.
    /// </summary>
    private WebApplicationFactory<Program> Factory(
        string database, bool? migrate = null, bool? seed = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Staging");

            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionStringFor(database));
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("ConnectionStrings:AzureBlob", _azurite.GetConnectionString());

            // The timers would otherwise start querying a database this test is
            // making a statement about, and their failures are not the subject.
            builder.UseSetting("BackgroundJobs:Enabled", "false");

            if (migrate is not null)
            {
                builder.UseSetting("Database:MigrateOnStartup", migrate.Value ? "true" : "false");
            }

            if (seed is not null)
            {
                builder.UseSetting("Database:SeedOnStartup", seed.Value ? "true" : "false");
            }
        });
    }

    /// <summary>The failure that a deployment actually hit, from the other side.</summary>
    [Fact]
    public async Task A_deployment_that_asks_for_a_schema_gets_one_whatever_the_environment_is_called()
    {
        const string database = "RoomRaahStartupMigrated";

        using var factory = Factory(database, migrate: true, seed: false);
        factory.CreateClient();

        (await DatabaseExistsAsync(database)).ShouldBeTrue();

        // Not merely "a database exists" — a schema, with the tables the product needs.
        (await CountAsync(database, "AppUsers")).ShouldBe(0);
        (await CountAsync(database, "Properties")).ShouldBe(0);
    }

    /// <summary>
    /// The combination that had no way to be expressed before: a real deployment wants
    /// its tables and emphatically does not want accounts with a published password.
    /// </summary>
    [Fact]
    public async Task Migrating_does_not_drag_the_demo_accounts_in_with_it()
    {
        const string database = "RoomRaahStartupNoSeed";

        using var factory = Factory(database, migrate: true, seed: false);
        factory.CreateClient();

        (await CountAsync(database, "AppUsers")).ShouldBe(0);
        (await CountAsync(database, "Properties")).ShouldBe(0);
    }

    /// <summary>
    /// A test deployment asks for both, and gets both — which is what makes the box a
    /// frontend developer can sign in to on the first boot.
    /// </summary>
    [Fact]
    public async Task A_test_deployment_that_asks_for_both_can_be_signed_into()
    {
        const string database = "RoomRaahStartupSeeded";

        using var factory = Factory(database, migrate: true, seed: true);
        factory.CreateClient();

        (await CountAsync(database, "AppUsers")).ShouldBeGreaterThan(0);
        (await CountAsync(database, "Properties")).ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// And nothing changed for anybody who was not asking. Without the switches the
    /// behaviour is exactly what it was — off outside Development — so a host that
    /// boots for a configuration question still does not need a database.
    /// </summary>
    [Fact]
    public async Task Saying_nothing_still_touches_nothing()
    {
        const string database = "RoomRaahStartupUntouched";

        using var factory = Factory(database);
        factory.CreateClient();

        (await DatabaseExistsAsync(database)).ShouldBeFalse();
    }
}
