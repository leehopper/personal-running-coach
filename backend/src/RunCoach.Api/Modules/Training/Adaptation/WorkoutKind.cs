using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Classifies a <see cref="WorkoutType"/> as a "key" (quality or long) session —
/// Tempo, Interval, Repetition, or LongRun — versus an easy/recovery/cross-train day.
/// Shared by the deviation engine and the micro-adjust planner so the key-workout
/// definition lives in one place (Slice 3 PR2 / Unit 1).
/// </summary>
internal static class WorkoutKind
{
    /// <summary>Whether the workout type is a key (quality/long) session.</summary>
    public static bool IsKey(WorkoutType type) =>
        type is WorkoutType.Tempo
            or WorkoutType.Interval
            or WorkoutType.Repetition
            or WorkoutType.LongRun;
}
