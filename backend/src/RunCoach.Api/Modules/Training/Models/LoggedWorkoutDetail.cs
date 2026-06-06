namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// A recent logged workout in the shape the coaching <c>ContextAssembler</c>
/// needs to render a compact Layer-1 one-liner (DEC-076 / slice-2b brainstorm
/// D). Distinct from the test-profile <see cref="WorkoutSummary"/>: it carries
/// the open metrics bag and freeform note from a real <c>WorkoutLog</c>, and
/// its rendering inlines present metrics with an explicit "(no HR/RPE)" absence
/// marker. Decoupled from the EF entity — the live mapping (entity → this view)
/// lands with the Slice 3/4 consumer that wires recent logs into the prompt.
/// </summary>
/// <param name="OccurredOn">The local date the run took place.</param>
/// <param name="WorkoutType">
/// The workout type label shown in the one-liner (e.g. "Easy", "Tempo"); a
/// generic label such as "Run" for off-plan logs with no prescribed type.
/// </param>
/// <param name="Distance">The distance covered.</param>
/// <param name="Duration">The elapsed running duration.</param>
/// <param name="Metrics">
/// Present metrics as canonical wire key → display value (e.g.
/// <c>{ "hrAvg": "148", "rpe": "7" }</c>). Only keys with
/// <see cref="Training.Constants.WorkoutMetricKeys.Metadata"/> render; absent
/// effort signals (HR/RPE) drive the "(no HR/RPE)" marker.
/// </param>
/// <param name="Notes">Freeform "what happened" note, or null/blank when none.</param>
public sealed record LoggedWorkoutDetail(
    DateOnly OccurredOn,
    string WorkoutType,
    Distance Distance,
    Duration Duration,
    IReadOnlyDictionary<string, string> Metrics,
    string? Notes);
