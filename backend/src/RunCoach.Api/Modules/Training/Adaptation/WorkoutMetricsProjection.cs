using System.Text.Json;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Projects the open <c>WorkoutLog.Metrics</c> <c>jsonb</c> string (canonical wire
/// key → <see cref="JsonElement"/> value, DEC-072) into the display dictionary
/// shape (<c>IReadOnlyDictionary&lt;string, string&gt;</c>) that
/// <c>LoggedWorkoutDetail</c> carries and <c>ISafetyGate.Classify</c> scans.
/// Scalar values only: strings pass through verbatim, numbers and booleans keep
/// their literal JSON text (culture-free), and non-scalar values (arrays,
/// objects, nulls) are dropped — they are not display values and carry no
/// free-text safety surface (splits live in their own typed column).
/// </summary>
internal static class WorkoutMetricsProjection
{
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Converts the raw metrics JSON into display key/value pairs.
    /// </summary>
    /// <param name="metricsJson">
    /// The raw <c>jsonb</c> string off the entity; null/empty means no optional
    /// metrics were logged.
    /// </param>
    /// <returns>The display dictionary; empty (never null) when no scalar metrics exist.</returns>
    /// <exception cref="JsonException">
    /// The stored string is not a JSON object. The API owns this column
    /// (DEC-072), so a malformed row is data corruption and must fail loudly
    /// rather than silently skipping the safety-gate scan of its free-text
    /// values.
    /// </exception>
    public static IReadOnlyDictionary<string, string> ToDisplayMetrics(string? metricsJson)
    {
        if (string.IsNullOrEmpty(metricsJson))
        {
            return Empty;
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metricsJson);
        if (raw is null || raw.Count == 0)
        {
            return Empty;
        }

        var display = new Dictionary<string, string>(raw.Count, StringComparer.Ordinal);
        foreach (var (key, value) in raw)
        {
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                _ => null,
            };

            if (text is not null)
            {
                display[key] = text;
            }
        }

        return display;
    }
}
