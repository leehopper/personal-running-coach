using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The deterministic comparison of a logged workout's actuals against its frozen
/// <see cref="WorkoutPrescriptionSnapshot"/> (Slice 3 PR2 / Unit 1). Pure data —
/// the escalation classifier interprets these signals against
/// <see cref="AdaptationThresholds"/>; this record carries no policy.
/// </summary>
/// <param name="OccurredOn">The calendar day the logged workout occurred on.</param>
/// <param name="CompletionStatus">How fully the runner completed the workout.</param>
/// <param name="IsKeyWorkout">
/// Whether the prescribed workout is a quality/long session (Tempo, Interval,
/// Repetition, or LongRun) rather than an easy/recovery/cross-train day.
/// </param>
/// <param name="DistanceDeviationPercent">
/// Signed percentage of actual-vs-prescribed distance: <c>(actual - prescribed) / prescribed * 100</c>.
/// Negative is short of plan; positive is over.
/// </param>
/// <param name="DurationDeviationPercent">
/// Signed percentage of actual-vs-prescribed duration, same sign convention as
/// <paramref name="DistanceDeviationPercent"/>.
/// </param>
/// <param name="PaceBand">Where the derived pace falls relative to the prescribed Fast/Slow band.</param>
/// <param name="PaceDeviationSecondsPerKm">
/// Signed magnitude (sec/km) by which the derived pace misses the nearest band
/// bound: positive when slower than the Slow bound, negative when faster than the
/// Fast bound, and zero when inside the band or when the pace is
/// <see cref="PaceBandMembership.Unknown"/>.
/// </param>
public sealed record DeviationResult(
    DateOnly OccurredOn,
    CompletionStatus CompletionStatus,
    bool IsKeyWorkout,
    double DistanceDeviationPercent,
    double DurationDeviationPercent,
    PaceBandMembership PaceBand,
    double PaceDeviationSecondsPerKm);
