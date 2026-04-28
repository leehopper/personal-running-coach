using Microsoft.Extensions.Logging;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> that records every emitted
/// entry, so tests can assert against structured properties of the log output
/// (level, exception, formatted message) and use the entry list as a progress
/// signal for condition-based waits in background-service tests.
/// </summary>
internal sealed class CapturingLogger<TCategoryName> : ILogger<TCategoryName>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
    }
}
