using System.Text.Json;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Read projection of a logged workout for the history surface (slice-2b Unit 4).
/// Mirrors the actual-run fields the runner recorded: required core fields, the
/// open optional-metrics bag (rehydrated from the entity's <c>jsonb</c> string),
/// the freeform note, and the typed display-only splits. The server-authoritative
/// prescription snapshot is intentionally not surfaced here at MVP-0 — it feeds
/// Slice-3 deterministic adaptation server-side, not the raw history view.
/// </summary>
public sealed record WorkoutLogDto(
    Guid WorkoutLogId,
    DateOnly OccurredOn,
    double DistanceMeters,
    double DurationSeconds,
    CompletionStatus CompletionStatus,
    string? Notes,
    IReadOnlyDictionary<string, JsonElement>? Metrics,
    IReadOnlyList<WorkoutLogSplitDto>? Splits);
