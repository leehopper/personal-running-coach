using System.Collections.Immutable;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A phase within a macro training plan (e.g., base, build, peak, taper).
/// </summary>
public sealed record PlanPhase(
    string PhaseType,
    DateOnly StartDate,
    DateOnly EndDate,
    DecimalRange WeeklyDistanceRangeKm,
    string IntensityDistribution,
    ImmutableArray<string> AllowedWorkoutTypes,
    string? Notes);
