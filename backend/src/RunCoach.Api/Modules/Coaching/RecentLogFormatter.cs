using System.Globalization;
using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Constants;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Renders a recent logged workout (<see cref="LoggedWorkoutDetail"/>) as a
/// compact, single-line Layer-1 entry for the coaching training-history block
/// (DEC-076 / slice-2b brainstorm D). Shape:
/// <c>date | type | dist km | dur min | pace/km | &lt;metrics&gt; | &lt;note&gt;</c>.
/// Present metrics are inlined with labels/units from the shared
/// <see cref="WorkoutMetricKeys.Metadata"/> source; effort signals (HR, RPE)
/// render first and, when none are present, an explicit
/// <see cref="NoEffortSignalsMarker"/> is emitted; peripheral and contextual
/// metrics are silently omitted when absent. The note is collapsed to a single
/// line so the entry never spans multiple lines (the assembler is a Layer-1
/// compression engine — verbose multi-line blocks would fight the token
/// budget's truncation cascade).
/// </summary>
internal static class RecentLogFormatter
{
    /// <summary>
    /// Marker emitted when a logged workout records neither heart rate nor RPE
    /// — the absence of effort signal is itself coaching signal (DEC-076).
    /// </summary>
    internal const string NoEffortSignalsMarker = "(no HR/RPE)";

    private const string FieldSeparator = " | ";

    /// <summary>
    /// Renders a single logged workout as a compact one-line training-history
    /// entry. The output never contains a line break.
    /// </summary>
    internal static string FormatWorkoutDetail(LoggedWorkoutDetail log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var fields = new List<string>
        {
            log.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ToSingleLine(log.WorkoutType),
            string.Create(CultureInfo.InvariantCulture, $"{FormatKilometers(log.Distance)} km"),
            string.Create(CultureInfo.InvariantCulture, $"{FormatMinutes(log.Duration)} min"),
        };

        var pace = FormatPace(log.Distance, log.Duration);
        if (pace is not null)
        {
            fields.Add(pace);
        }

        // Metrics segment is always present — at minimum the "(no HR/RPE)" marker.
        fields.Add(FormatMetrics(log.Metrics));

        if (!string.IsNullOrWhiteSpace(log.Notes))
        {
            fields.Add(ToSingleLine(log.Notes));
        }

        return string.Join(FieldSeparator, fields);
    }

    private static string FormatKilometers(Distance distance) =>
        distance.Kilometers.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatMinutes(Duration duration) =>
        ((int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Renders the pace as <c>m:ss/km</c> (or <c>h:mm:ss/km</c>), or null when
    /// distance/duration cannot yield a meaningful pace (e.g. a skipped run).
    /// </summary>
    private static string? FormatPace(Distance distance, Duration duration)
    {
        if (PaceDerivation.TryDerive(distance, duration) is not { } pace)
        {
            return null;
        }

        var ts = pace.ToTimeSpan();
        var clock = ts.TotalHours >= 1
            ? ts.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : ts.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{clock}/km");
    }

    /// <summary>
    /// Builds the comma-joined metrics segment: present metrics in
    /// <see cref="WorkoutMetricKeys.DisplayOrder"/>, effort signals first, with
    /// the <see cref="NoEffortSignalsMarker"/> substituted when no effort signal
    /// is present. Unknown-to-metadata keys are skipped.
    /// </summary>
    private static string FormatMetrics(IReadOnlyDictionary<string, string> metrics)
    {
        var effort = new List<string>();
        var other = new List<string>();

        foreach (var key in WorkoutMetricKeys.DisplayOrder)
        {
            if (!metrics.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var meta = WorkoutMetricKeys.Metadata[key];
            var safeValue = ToSingleLine(value);
            var rendered = meta.Unit.Length == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{meta.Label} {safeValue}")
                : string.Create(CultureInfo.InvariantCulture, $"{meta.Label} {safeValue} {meta.Unit}");

            if (meta.Category == MetricCategory.Effort)
            {
                effort.Add(rendered);
            }
            else
            {
                other.Add(rendered);
            }
        }

        if (effort.Count == 0)
        {
            effort.Add(NoEffortSignalsMarker);
        }

        return string.Join(", ", effort.Concat(other));
    }

    /// <summary>
    /// Collapses any line breaks to single spaces so a freeform note (or
    /// free-text metric) cannot split the one-liner across multiple lines.
    /// </summary>
    private static string ToSingleLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
