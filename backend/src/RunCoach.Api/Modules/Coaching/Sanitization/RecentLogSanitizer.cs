using RunCoach.Api.Modules.Training.Constants;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Default <see cref="IRecentLogSanitizer"/> — sanitizes the note and free-text
/// metric values of a <see cref="LoggedWorkoutDetail"/> via the DEC-059
/// <see cref="IPromptSanitizer"/> under the
/// <see cref="PromptSection.TrainingHistoryWorkoutNote"/> policy. Stateless;
/// registered as a singleton.
/// </summary>
public sealed class RecentLogSanitizer(IPromptSanitizer sanitizer) : IRecentLogSanitizer
{
    private static readonly string[] FreeTextMetricKeys =
        [WorkoutMetricKeys.Weather, WorkoutMetricKeys.Terrain];

    private readonly IPromptSanitizer _sanitizer = sanitizer;

    /// <inheritdoc />
    public async ValueTask<LoggedWorkoutDetail> SanitizeAsync(
        LoggedWorkoutDetail detail,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var notes = detail.Notes;
        if (!string.IsNullOrEmpty(notes))
        {
            var sanitizedNote = await _sanitizer
                .SanitizeAsync(notes, PromptSection.TrainingHistoryWorkoutNote, ct)
                .ConfigureAwait(false);
            notes = sanitizedNote.Sanitized;
        }

        var metrics = await SanitizeFreeTextMetricsAsync(detail.Metrics, ct).ConfigureAwait(false);

        return detail with { Notes = notes, Metrics = metrics };
    }

    private async ValueTask<IReadOnlyDictionary<string, string>> SanitizeFreeTextMetricsAsync(
        IReadOnlyDictionary<string, string> metrics,
        CancellationToken ct)
    {
        if (!metrics.ContainsKey(WorkoutMetricKeys.Weather) && !metrics.ContainsKey(WorkoutMetricKeys.Terrain))
        {
            return metrics;
        }

        var sanitized = new Dictionary<string, string>(metrics, StringComparer.Ordinal);
        foreach (var key in FreeTextMetricKeys)
        {
            if (sanitized.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                var result = await _sanitizer
                    .SanitizeAsync(value, PromptSection.TrainingHistoryWorkoutNote, ct)
                    .ConfigureAwait(false);
                sanitized[key] = result.Sanitized;
            }
        }

        return sanitized;
    }
}
