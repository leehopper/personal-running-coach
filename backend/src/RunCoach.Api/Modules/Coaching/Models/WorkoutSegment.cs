using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A segment within a structured workout (e.g., warmup, work interval, recovery).
/// </summary>
public sealed record WorkoutSegment(
    string SegmentType,
    decimal? DistanceKm,
    int? DurationMinutes,
    PaceRange? TargetPace,
    int? Repetitions,
    string? Notes);
