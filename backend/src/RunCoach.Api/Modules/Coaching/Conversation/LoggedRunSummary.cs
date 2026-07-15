using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The structured actuals of a confirmed conversational log, carried on the
/// confirm-ack <see cref="CoachMessagePosted"/> turn (Slice 3, DEC-091) so the
/// durable receipt D6 requires is reconstructable straight from the timeline —
/// no client-side merge against the separately-paginated workout-log history.
/// Additive-nullable on the event: existing persisted turns hydrate
/// <see langword="null"/> on replay, no upcaster needed.
/// </summary>
/// <param name="WorkoutLogId">The id of the committed <c>WorkoutLog</c> this turn acknowledges.</param>
/// <param name="DistanceKm">The confirmed distance in kilometers (server-converted from the runner-stated unit).</param>
/// <param name="DurationSeconds">The confirmed elapsed time in seconds.</param>
/// <param name="OccurredOn">The calendar date the workout occurred on.</param>
/// <param name="CompletionStatus">How completely the workout was carried out.</param>
public sealed record LoggedRunSummary(
    Guid WorkoutLogId,
    double DistanceKm,
    double DurationSeconds,
    DateOnly OccurredOn,
    CompletionStatus CompletionStatus);
