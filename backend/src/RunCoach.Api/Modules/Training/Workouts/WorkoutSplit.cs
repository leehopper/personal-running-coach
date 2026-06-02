namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// One recorded lap/split within a logged workout (DEC-072). Primitive-typed so
/// it serializes cleanly into the entity's typed <c>Splits</c> <c>jsonb</c>
/// column; display-only at MVP-0 (no aggregation server-side).
/// </summary>
/// <param name="Index">1-based split index in run order.</param>
/// <param name="DistanceMeters">Split distance in meters.</param>
/// <param name="DurationSeconds">Split elapsed time in seconds.</param>
/// <param name="PaceSecPerKm">Split pace in seconds per kilometer.</param>
/// <param name="AverageHeartRate">Optional average heart rate over the split, bpm.</param>
public sealed record WorkoutSplit(
    int Index,
    double DistanceMeters,
    double DurationSeconds,
    double PaceSecPerKm,
    int? AverageHeartRate);
