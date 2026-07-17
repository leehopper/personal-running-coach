using System.Text.Json;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Read projection of a logged workout for the history surface (slice-2b Unit 4).
/// Mirrors the actual-run fields the runner recorded: required core fields, the
/// open optional-metrics bag (rehydrated from the entity's <c>jsonb</c> string),
/// the freeform note, and the typed display-only splits. The full prescription
/// snapshot (distance/duration/pace) is intentionally not surfaced here — it feeds
/// server-side deterministic adaptation, not the raw history view; only a
/// point-in-time label of it is exposed via <c>IsOnPlan</c> and
/// <c>PrescribedWorkoutType</c> (Slice 4 D4).
/// </summary>
/// <param name="IsOnPlan">
/// <c>true</c> when this log's entity carries a stored prescription snapshot
/// (the run matched a scheduled plan slot at create time); <c>false</c> for an
/// off-plan or legacy run.
/// </param>
/// <param name="PrescribedWorkoutType">
/// The frozen <c>WorkoutType</c> enum name the snapshot recorded at create time
/// (e.g. <c>"Tempo"</c>), or <c>null</c> exactly when <paramref name="IsOnPlan"/>
/// is <c>false</c>. Frozen means a later plan restructure cannot retroactively
/// change what an already-logged row displays.
/// </param>
public sealed record WorkoutLogDto(
    Guid WorkoutLogId,
    DateOnly OccurredOn,
    double DistanceMeters,
    double DurationSeconds,
    CompletionStatus CompletionStatus,
    string? Notes,
    IReadOnlyDictionary<string, JsonElement>? Metrics,
    IReadOnlyList<WorkoutLogSplitDto>? Splits,
    bool IsOnPlan,
    string? PrescribedWorkoutType);
