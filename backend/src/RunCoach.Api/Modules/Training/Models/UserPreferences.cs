using System.Collections.Immutable;

namespace RunCoach.Api.Modules.Training.Models;

/// <summary>
/// User's scheduling and training preferences.
/// </summary>
public sealed record UserPreferences(
    ImmutableArray<DayOfWeek> PreferredRunDays,
    DayOfWeek LongRunDay,
    int MaxRunDaysPerWeek,
    string PreferredUnits,
    int? AvailableTimePerRunMinutes,
    ImmutableArray<string> Constraints);
