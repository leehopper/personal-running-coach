namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Wolverine command that evaluates a just-committed <c>WorkoutLog</c> for plan
/// adaptation (Slice 3 § Unit 5, DEC-012/DEC-060/DEC-073). Invoked synchronously
/// by the log-create flow via <c>bus.InvokeForTenantAsync</c> AFTER the EF create
/// has committed — the relational write is never inside the handler. Handled by
/// <see cref="EvaluateAdaptationHandler"/>.
/// </summary>
/// <param name="WorkoutLogId">
/// The committed log to evaluate. Also the idempotency key: one adaptation
/// evaluation commits per log, ever.
/// </param>
/// <param name="UserId">The owning runner; scopes the log read so a forged id can never evaluate another user's log.</param>
public sealed record EvaluateAdaptationCommand(Guid WorkoutLogId, Guid UserId);
