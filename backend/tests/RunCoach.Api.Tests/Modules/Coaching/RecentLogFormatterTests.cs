using FluentAssertions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Training.Constants;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching;

public class RecentLogFormatterTests
{
    private static readonly DateOnly RunDate = new(2026, 6, 1);

    [Fact]
    public void FormatWorkoutDetail_WithCoreFields_RendersSingleLineWithDateTypeDistanceDurationPace()
    {
        // Arrange — 8 km in 40 min = 5:00/km.
        var log = Log("Tempo", 8, 40, Metrics((WorkoutMetricKeys.HrAvg, "148"), (WorkoutMetricKeys.Rpe, "7")));

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert
        actual.Should().NotContain("\n").And.NotContain("\r");
        actual.Should().Contain("2026-06-01");
        actual.Should().Contain("Tempo");
        actual.Should().Contain("8 km");
        actual.Should().Contain("40 min");
        actual.Should().Contain("5:00/km");
    }

    [Fact]
    public void FormatWorkoutDetail_WithHrAndRpe_InlinesBothEffortSignals()
    {
        // Arrange
        var log = Log("Tempo", 8, 40, Metrics((WorkoutMetricKeys.HrAvg, "148"), (WorkoutMetricKeys.Rpe, "7")));

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert
        actual.Should().Contain("HR 148");
        actual.Should().Contain("RPE 7");
        actual.Should().NotContain(RecentLogFormatter.NoEffortSignalsMarker);
    }

    [Fact]
    public void FormatWorkoutDetail_WithNote_AppendsNoteText()
    {
        // Arrange
        const string note = "legs felt heavy in the last mile";
        var log = Log("Easy", 6, 36, Metrics((WorkoutMetricKeys.Rpe, "5")), note);

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert
        actual.Should().Contain(note);
    }

    [Fact]
    public void FormatWorkoutDetail_WithNeitherHrNorRpe_EmitsAbsenceMarker()
    {
        // Arrange — no effort signals recorded.
        var log = Log("Easy", 6, 36, Metrics());

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert
        actual.Should().Contain(RecentLogFormatter.NoEffortSignalsMarker);
    }

    [Fact]
    public void FormatWorkoutDetail_WithEffortButNoPeripheral_OmitsPeripheralTextAndAbsenceMarker()
    {
        // Arrange — HR + RPE present, no cadence / power / elevation.
        var log = Log("Tempo", 8, 40, Metrics((WorkoutMetricKeys.HrAvg, "148"), (WorkoutMetricKeys.Rpe, "7")));

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — peripheral metrics are silently omitted, no peripheral marker.
        actual.Should().NotContain("cadence");
        actual.Should().NotContain("power");
        actual.Should().NotContain("elev");
        actual.Should().NotContain(RecentLogFormatter.NoEffortSignalsMarker);
    }

    [Fact]
    public void FormatWorkoutDetail_PeripheralMetric_RendersLabelAndUnitFromMetadata()
    {
        // Arrange — power is a peripheral metric carrying a unit in the metadata.
        var log = Log(
            "Tempo",
            8,
            40,
            Metrics((WorkoutMetricKeys.HrAvg, "148"), (WorkoutMetricKeys.Rpe, "7"), (WorkoutMetricKeys.Power, "290")));
        var powerMeta = WorkoutMetricKeys.Metadata[WorkoutMetricKeys.Power];

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — printed label + unit come from the shared metadata entry.
        actual.Should().Contain($"{powerMeta.Label} 290 {powerMeta.Unit}");
    }

    [Fact]
    public void FormatWorkoutDetail_NoEffortButPeripheralPresent_EmitsAbsenceMarkerAndPeripheral()
    {
        // Arrange — cadence present, no HR/RPE.
        var log = Log("Easy", 6, 36, Metrics((WorkoutMetricKeys.Cadence, "178")));
        var cadenceMeta = WorkoutMetricKeys.Metadata[WorkoutMetricKeys.Cadence];

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — absence marker AND the present peripheral metric.
        actual.Should().Contain(RecentLogFormatter.NoEffortSignalsMarker);
        actual.Should().Contain($"{cadenceMeta.Label} 178 {cadenceMeta.Unit}");
    }

    [Fact]
    public void FormatWorkoutDetail_NoteWithNewlinesAndQuotes_RemainsSingleLine()
    {
        // Arrange — a multi-line note with quote characters.
        const string note = "first mile rough\nthen \"clicked\" into rhythm\r\nnegative split";
        var log = Log("Tempo", 8, 40, Metrics((WorkoutMetricKeys.Rpe, "7")), note);

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — collapsed to one line, content preserved in readable form.
        actual.Should().NotContain("\n").And.NotContain("\r");
        actual.Should().Contain("first mile rough");
        actual.Should().Contain("then \"clicked\" into rhythm");
        actual.Should().Contain("negative split");
    }

    [Fact]
    public void FormatWorkoutDetail_WorkoutTypeWithNewlines_RemainsSingleLine()
    {
        // Arrange — a workout type carrying line breaks must not split the entry.
        var log = Log("Tempo\nintervals\r\nset", 8, 40, Metrics((WorkoutMetricKeys.Rpe, "7")));

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — collapsed to one line, content preserved in readable form.
        actual.Should().NotContain("\n").And.NotContain("\r");
        actual.Should().Contain("Tempo intervals set");
    }

    [Fact]
    public void FormatWorkoutDetail_EmptyMetricsAndNoNote_EmitsOnlyAbsenceMarkerInMetricsSegment()
    {
        // Arrange — a bare log: distance + duration only.
        var log = Log("Easy", 5, 30, Metrics());

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — the bare-vs-rich distinction: absence marker, no other metric text.
        actual.Should().Contain(RecentLogFormatter.NoEffortSignalsMarker);
        actual.Should().NotContain("cadence");
        actual.Should().NotContain("power");
    }

    [Fact]
    public void FormatWorkoutDetail_ZeroDistance_OmitsPaceSegment()
    {
        // Arrange — a skipped/zero-distance log has no meaningful pace.
        var log = new LoggedWorkoutDetail(
            RunDate,
            "Easy",
            Distance.FromKilometers(0),
            Duration.FromMinutes(30),
            Metrics(),
            Notes: null);

        // Act
        var actual = RecentLogFormatter.FormatWorkoutDetail(log);

        // Assert — no pace token, but duration still shown.
        actual.Should().NotContain("/km");
        actual.Should().Contain("30 min");
    }

    private static LoggedWorkoutDetail Log(
        string type,
        double km,
        double minutes,
        IReadOnlyDictionary<string, string> metrics,
        string? notes = null)
    {
        return new LoggedWorkoutDetail(
            RunDate,
            type,
            Distance.FromKilometers(km),
            Duration.FromMinutes(minutes),
            metrics,
            notes);
    }

    private static Dictionary<string, string> Metrics(params (string Key, string Value)[] entries)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }

        return dict;
    }
}
