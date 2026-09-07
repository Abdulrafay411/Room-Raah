using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoomRaah.IntegrationTests.Infrastructure;
using Serilog;
using Shouldly;

namespace RoomRaah.IntegrationTests.Contracts;

/// <summary>
/// A host has to keep its logging to itself. Serilog's default is the opposite: the
/// logger a host builds is assigned to the process-wide <c>Serilog.Log</c>, and every
/// <c>ILogger&lt;T&gt;</c> resolves through that field at the moment it writes. A
/// second host in the same process then owns the first host's logging, which is how a
/// suite that boots its own factory silently emptied another suite's log.
/// </summary>
[Collection(ApiCollection.Name)]
public class LoggerOwnershipTests
{
    private readonly RoomRaahApiFactory _factory;

    public LoggerOwnershipTests(RoomRaahApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void The_host_does_not_claim_the_process_wide_logger()
    {
        var mine = _factory.Services.GetRequiredService<Serilog.ILogger>();

        Serilog.Log.Logger.ShouldNotBeSameAs(mine);
    }

    [Fact]
    public void A_logger_from_this_host_still_writes_here_after_the_static_one_is_replaced()
    {
        var marker = $"logger-ownership-{Guid.NewGuid():N}";
        var previous = Serilog.Log.Logger;

        try
        {
            // What a second WebApplicationFactory does on the way up, with a logger
            // that has no sinks: anything following the static field goes nowhere.
            Serilog.Log.Logger = new LoggerConfiguration().CreateLogger();

            _factory.Services
                .GetRequiredService<ILogger<LoggerOwnershipTests>>()
                .LogInformation("Marker {Marker}", marker);
        }
        finally
        {
            Serilog.Log.Logger = previous;
        }

        _factory.Logs.Mentioning(marker).ShouldNotBeEmpty(
            "Something replaced the process-wide logger and this host's events followed it there.");
    }
}
