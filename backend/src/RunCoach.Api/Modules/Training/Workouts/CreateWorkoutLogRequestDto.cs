using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Request body for <c>POST /api/v1/workouts/logs</c> (slice-2b Unit 3 / DEC-076).
/// Carries the runner's actuals plus a client-generated idempotency key. It
/// deliberately carries NO prescription fields: the server resolves the
/// prescription snapshot itself from the run's date and the active plan, never
/// trusting client-sent prescribed values.
/// </summary>
/// <param name="IdempotencyKey">
/// Client-generated idempotency key (typically <c>crypto.randomUUID()</c>) the
/// frontend re-sends on retry. Marked <see cref="JsonRequiredAttribute"/> so
/// System.Text.Json refuses to under-post a default <see cref="Guid"/> when the
/// field is omitted.
/// </param>
/// <param name="OccurredOn">The calendar date the run occurred — the
/// prescription-matching anchor (DEC-076).</param>
/// <param name="DistanceMeters">Total distance run, in meters.</param>
/// <param name="DurationSeconds">Total elapsed duration, in seconds.</param>
/// <param name="CompletionStatus">How fully the workout was completed.</param>
/// <param name="Notes">Optional freeform "what happened" note. No server-side length cap.</param>
/// <param name="Metrics">Optional open metrics bag (DEC-072 canonical keys); a
/// <c>z.record</c>/<c>additionalProperties</c> map persisted verbatim to the
/// entity's <c>jsonb</c> column. Null when no optional metrics were provided.</param>
/// <param name="Splits">Optional typed per-lap splits. Null when none.</param>
public sealed record CreateWorkoutLogRequestDto(
    [property: JsonRequired] Guid IdempotencyKey,
    [property: JsonRequired] DateOnly OccurredOn,
    [property: JsonRequired] double DistanceMeters,
    [property: JsonRequired] double DurationSeconds,
    [property: JsonRequired] CompletionStatus CompletionStatus,
    string? Notes,
    IReadOnlyDictionary<string, JsonElement>? Metrics,
    IReadOnlyList<WorkoutLogSplitDto>? Splits);
