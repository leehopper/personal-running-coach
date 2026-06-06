namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Wire shape for one recorded lap/split on a create-workout-log request
/// (slice-2b Unit 3). Mirrors <see cref="WorkoutSplit"/> as primitives so it
/// flows through the OpenAPI → Orval/RTK codegen pipeline (DEC-066); the server
/// maps it onto the entity's typed <c>Splits</c> column, which re-validates the
/// per-field invariants on construction.
/// </summary>
/// <param name="Index">1-based split index in run order.</param>
/// <param name="DistanceMeters">Split distance in meters.</param>
/// <param name="DurationSeconds">Split elapsed time in seconds.</param>
/// <param name="PaceSecPerKm">Split pace in seconds per kilometer.</param>
/// <param name="AverageHeartRate">Optional average heart rate over the split, bpm.</param>
public sealed record WorkoutLogSplitDto(
    int Index,
    double DistanceMeters,
    double DurationSeconds,
    double PaceSecPerKm,
    int? AverageHeartRate);
