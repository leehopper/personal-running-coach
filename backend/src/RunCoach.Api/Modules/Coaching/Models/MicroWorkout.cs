using System.Collections.Immutable;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A specific workout generated on demand, with detailed structure,
/// pacing guidance, and coaching notes.
/// </summary>
public sealed record MicroWorkout(
    Guid WorkoutId,
    DateOnly Date,
    string WorkoutType,
    decimal? TargetDistanceKm,
    int? TargetDurationMinutes,
    PaceRange? TargetPace,
    ImmutableArray<WorkoutSegment> Structure,
    string CoachingNotes,
    string? WarmupNotes,
    string? CooldownNotes);
