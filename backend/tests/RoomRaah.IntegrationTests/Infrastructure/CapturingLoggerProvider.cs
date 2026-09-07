using Microsoft.Extensions.Logging;

namespace RoomRaah.IntegrationTests.Infrastructure;

/// <summary>
/// Collects what the API actually logged. Serilog forwards to registered providers
/// because <c>writeToProviders</c> is on, so this sees the real events rather than a
/// reconstruction of them.
/// </summary>
public class CapturingLoggerProvider : ILoggerProvider
{
    public record Entry(string Category, LogLevel Level, string Message);

    private readonly List<Entry> _entries = [];
    private readonly Lock _gate = new();

    public ILogger CreateLogger(string categoryName) => new Capturing(this, categoryName);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void Record(Entry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    public Entry[] Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public Entry[] RequestsFor(string path)
    {
        return Snapshot()
            .Where(e => e.Message.StartsWith("HTTP ") && e.Message.Contains(path))
            .ToArray();
    }

    /// <summary>
    /// Everything logged that mentions the given fragment, access log and all. Tests
    /// that need to see one request's whole trail pass something unique to it, because
    /// the suites share a host and every one of them is writing to this at the time.
    /// </summary>
    public Entry[] Mentioning(string fragment)
    {
        return Snapshot()
            .Where(e => e.Message.Contains(fragment))
            .ToArray();
    }

    private class Capturing : ILogger
    {
        private readonly CapturingLoggerProvider _provider;
        private readonly string _category;

        public Capturing(CapturingLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Record(new Entry(_category, logLevel, formatter(state, exception)));
        }
    }
}
