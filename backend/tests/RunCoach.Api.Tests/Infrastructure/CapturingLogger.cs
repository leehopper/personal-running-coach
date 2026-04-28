using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> that records every emitted
/// entry, so tests can assert against structured properties of the log output
/// (level, exception, formatted message) and use the entry list as a progress
/// signal for condition-based waits in background-service tests.
/// Backed by <see cref="ConcurrentQueue{T}"/>: the SUT typically emits from a
/// background-service worker thread while the test thread enumerates, so a
/// non-thread-safe <see cref="List{T}"/> would race.
/// </summary>
internal sealed class CapturingLogger<TCategoryName> : ILogger<TCategoryName>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    /// <summary>
    /// Gets a thread-safe snapshot of the captured entries. Each call
    /// allocates — callers should snapshot once per assertion when iterating
    /// in tight loops.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

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
        _entries.Enqueue(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
    }
}
