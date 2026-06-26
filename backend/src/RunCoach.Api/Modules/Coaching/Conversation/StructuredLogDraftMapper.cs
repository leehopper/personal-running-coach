using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Maps a confirmed <see cref="StructuredLogDraft"/> onto the unchanged Slice 2b
/// <see cref="CreateWorkoutLogRequestDto"/> create contract (Slice 4B confirm-then-commit,
/// DEC-085 D4). The draft carries actuals only; the caller supplies the EF-row idempotency
/// key (DEC-077) and the server resolves the prescription on the create path — neither comes
/// from the LLM.
/// </summary>
public static class StructuredLogDraftMapper
{
    /// <summary>
    /// Builds a <see cref="CreateWorkoutLogRequestDto"/> from a confirmed draft.
    /// </summary>
    /// <param name="draft">The confirmed workout draft.</param>
    /// <param name="idempotencyKey">The client-generated EF-row idempotency key for the create.</param>
    /// <returns>A create request carrying the draft's SI-unit actuals; <c>Metrics</c>/<c>Splits</c> are null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draft"/> is null.</exception>
    public static CreateWorkoutLogRequestDto ToCreateWorkoutLogRequest(StructuredLogDraft draft, Guid idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new CreateWorkoutLogRequestDto(
            IdempotencyKey: idempotencyKey,
            OccurredOn: draft.OccurredOn,
            DistanceMeters: draft.DistanceMeters,
            DurationSeconds: draft.DurationSeconds,
            CompletionStatus: draft.CompletionStatus,
            Notes: draft.Notes,
            Metrics: null,
            Splits: null);
    }
}
