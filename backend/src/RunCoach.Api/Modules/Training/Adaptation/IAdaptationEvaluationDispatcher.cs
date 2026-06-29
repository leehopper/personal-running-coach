using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The shared post-create adaptation seam: synchronously evaluates a just-committed
/// <c>WorkoutLog</c> for plan adaptation via the Wolverine inline pipeline and maps the two
/// known lost-race surfaces to a retryable <c>Kind=Error</c> envelope rather than a 5xx
/// (Slice 3 § Unit 5 / DEC-073). Extracted from <c>WorkoutLogsController</c> so both the
/// form-logged create path and the Slice 4B conversational-logging confirm path run the
/// IDENTICAL dispatch + conflict mapping without drift.
/// </summary>
public interface IAdaptationEvaluationDispatcher
{
    /// <summary>
    /// Dispatches <see cref="EvaluateAdaptationCommand"/> for <paramref name="workoutLogId"/>
    /// under <paramref name="userId"/>'s Marten tenant and returns the resolved
    /// <see cref="AdaptationResponseDto"/>. A concurrency conflict that escapes the handler's
    /// bounded retries (a stream-version conflict or a duplicate idempotency-marker insert)
    /// is caught and mapped to a generic retryable <c>Kind=Error</c> envelope — the committed
    /// log must never be failed by a lost adaptation race. Any other fault propagates.
    /// </summary>
    /// <param name="workoutLogId">The committed log to evaluate (also the adaptation idempotency key).</param>
    /// <param name="userId">The owning runner; the Marten tenant and the log-read scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's envelope, or a retryable <c>Kind=Error</c> envelope on a lost race.</returns>
    Task<AdaptationResponseDto> EvaluateAsync(Guid workoutLogId, Guid userId, CancellationToken ct);
}
