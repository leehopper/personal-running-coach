using Microsoft.Extensions.Logging;

namespace RunCoach.Api.Tests.Infrastructure;

/// <summary>
/// Immutable record of a single <see cref="ILogger"/> emission captured by
/// <see cref="CapturingLogger{TCategoryName}"/>. Tests assert against the
/// recorded fields rather than reflection-shaped substitute received-call lists.
/// </summary>
internal sealed record LogEntry(LogLevel Level, EventId EventId, Exception? Exception, string Message);
