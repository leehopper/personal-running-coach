namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Supplies the app-local calendar "today". Wraps the injected
/// <see cref="System.TimeProvider"/> and the configured app time zone so callers
/// never reach for <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> — which can resolve
/// a day off near midnight and shift the whole generated plan calendar.
/// </summary>
public interface ILocalDateProvider
{
    /// <summary>Gets the current calendar date in the configured app time zone.</summary>
    DateOnly Today();
}
