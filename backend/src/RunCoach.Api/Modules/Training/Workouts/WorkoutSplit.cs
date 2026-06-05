namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// One recorded lap/split within a logged workout (DEC-072). Primitive-typed so
/// it serializes cleanly into the entity's typed <c>Splits</c> <c>jsonb</c>
/// column; display-only at MVP-0 (no aggregation server-side). Invariants are
/// enforced in the constructor — which also runs on JSON deserialization — so an
/// invalid split can be neither constructed in code nor materialized from storage.
/// </summary>
/// <param name="Index">1-based split index in run order; must be positive.</param>
/// <param name="DistanceMeters">Split distance in meters; must be positive.</param>
/// <param name="DurationSeconds">Split elapsed time in seconds; must be positive.</param>
/// <param name="PaceSecPerKm">Split pace in seconds per kilometer; must be positive.</param>
/// <param name="AverageHeartRate">Optional average heart rate over the split, bpm; positive when present.</param>
public sealed record WorkoutSplit(
    int Index,
    double DistanceMeters,
    double DurationSeconds,
    double PaceSecPerKm,
    int? AverageHeartRate)
{
    /// <summary>Gets the 1-based split index in run order.</summary>
    public int Index { get; init; } = Index > 0
        ? Index
        : throw new ArgumentOutOfRangeException(nameof(Index), Index, "Split index must be 1-based (positive).");

    /// <summary>Gets the split distance in meters.</summary>
    public double DistanceMeters { get; init; } = DistanceMeters > 0
        ? DistanceMeters
        : throw new ArgumentOutOfRangeException(nameof(DistanceMeters), DistanceMeters, "Split distance in meters must be positive.");

    /// <summary>Gets the split elapsed time in seconds.</summary>
    public double DurationSeconds { get; init; } = DurationSeconds > 0
        ? DurationSeconds
        : throw new ArgumentOutOfRangeException(nameof(DurationSeconds), DurationSeconds, "Split duration in seconds must be positive.");

    /// <summary>Gets the split pace in seconds per kilometer.</summary>
    public double PaceSecPerKm { get; init; } = PaceSecPerKm > 0
        ? PaceSecPerKm
        : throw new ArgumentOutOfRangeException(nameof(PaceSecPerKm), PaceSecPerKm, "Split pace in seconds per kilometer must be positive.");

    /// <summary>Gets the optional average heart rate over the split, bpm.</summary>
    public int? AverageHeartRate { get; init; } = AverageHeartRate is null or > 0
        ? AverageHeartRate
        : throw new ArgumentOutOfRangeException(nameof(AverageHeartRate), AverageHeartRate, "Average heart rate in bpm must be positive when present.");
}
