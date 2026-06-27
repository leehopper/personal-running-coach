namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Deterministic SI conversion for the runner-stated actuals in a
/// <see cref="StructuredLogDraft"/> (Slice 4B, DEC-085 D3). The intent classifier
/// reports what the runner said in their own units (value + unit, h/m/s components);
/// this pure, unit-tested helper does the conversion the LLM must never do (REVIEW.md
/// Architecture: distance/time conversions belong in the computation layer). Used by
/// <see cref="StructuredLogDraftMapper"/> when a confirmed draft is mapped onto the
/// SI-unit <c>CreateWorkoutLogRequestDto</c>.
/// </summary>
public static class WorkoutDraftUnitConverter
{
    /// <summary>Meters in one kilometer.</summary>
    public const double MetersPerKilometer = 1000d;

    /// <summary>Meters in one statute mile (exact, by international definition).</summary>
    public const double MetersPerMile = 1609.344d;

    private const double SecondsPerHour = 3600d;
    private const double SecondsPerMinute = 60d;

    /// <summary>
    /// Converts a runner-stated distance to meters.
    /// </summary>
    /// <param name="value">The distance magnitude the runner stated.</param>
    /// <param name="unit">The unit the runner stated it in.</param>
    /// <returns>The distance in meters.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unit"/> is not a defined member.</exception>
    public static double DistanceToMeters(double value, RunnerDistanceUnit unit) => unit switch
    {
        RunnerDistanceUnit.Meters => value,
        RunnerDistanceUnit.Kilometers => value * MetersPerKilometer,
        RunnerDistanceUnit.Miles => value * MetersPerMile,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown runner distance unit."),
    };

    /// <summary>
    /// Converts a runner-stated duration (hours/minutes/seconds components) to total seconds.
    /// </summary>
    /// <param name="hours">Whole hours the runner stated.</param>
    /// <param name="minutes">Whole minutes the runner stated.</param>
    /// <param name="seconds">Whole seconds the runner stated.</param>
    /// <returns>The total elapsed time in seconds.</returns>
    public static double DurationToSeconds(int hours, int minutes, int seconds) =>
        (hours * SecondsPerHour) + (minutes * SecondsPerMinute) + seconds;
}
