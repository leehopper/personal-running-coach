using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Wire shape of <see cref="LoggedRunSummary"/> carried on
/// <see cref="InteractiveTurnDto.LoggedRun"/> (Slice 3, DEC-091) — the durable
/// receipt the frontend renders on the confirm-ack coach turn.
/// </summary>
/// <param name="WorkoutLogId">The id of the committed <c>WorkoutLog</c> this turn acknowledges.</param>
/// <param name="DistanceKm">The confirmed distance in kilometers.</param>
/// <param name="DurationSeconds">The confirmed elapsed time in seconds.</param>
/// <param name="OccurredOn">The calendar date the workout occurred on.</param>
/// <param name="CompletionStatus">How completely the workout was carried out.</param>
public sealed record LoggedRunSummaryDto(
    Guid WorkoutLogId,
    double DistanceKm,
    double DurationSeconds,
    DateOnly OccurredOn,
    CompletionStatus CompletionStatus);
