using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Routes a recent logged workout's user-controlled free-text — the
/// <see cref="LoggedWorkoutDetail.Notes"/> and the free-text metric values
/// (weather / terrain) — through the DEC-059 <see cref="IPromptSanitizer"/>
/// before the detail reaches any assembled prompt (Slice 3 Unit 3 sanitizer
/// coverage). Closes the documented gap where <c>RecentLogFormatter</c> inlines
/// these fields un-sanitized. The <c>WorkoutLog</c> → <see cref="LoggedWorkoutDetail"/>
/// entity mapping that produces the input is the Slice 3/4 consumer's concern.
/// </summary>
public interface IRecentLogSanitizer
{
    /// <summary>
    /// Returns a copy of <paramref name="detail"/> with its note and free-text
    /// metric values sanitized for prompt assembly. Numeric metric values are
    /// passed through unchanged.
    /// </summary>
    /// <param name="detail">The recent-log view to sanitize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A sanitized copy of the detail.</returns>
    ValueTask<LoggedWorkoutDetail> SanitizeAsync(LoggedWorkoutDetail detail, CancellationToken ct = default);
}
