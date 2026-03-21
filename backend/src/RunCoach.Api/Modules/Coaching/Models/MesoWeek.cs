using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// A weekly training template within a plan phase, including
/// daily slot assignments and volume targets.
/// </summary>
public sealed record MesoWeek(
    int WeekNumber,
    string PhaseType,
    bool IsDeloadWeek,
    decimal WeeklyTargetKm,
    ImmutableArray<DaySlot> Days);
