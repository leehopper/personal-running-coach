namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Application-level clock configuration. Binds the <c>App</c> configuration section.
/// At MVP-0 the app runs in a single configured time zone (solo + family user); the
/// per-user time zone is deliberately deferred (DEC-076 / DEC-082). The zone resolves
/// only <em>which wall-calendar day is "now"</em> for plan anchoring — calendar dates
/// themselves stay timezone-free <see cref="System.DateOnly"/> and are never converted.
/// </summary>
public sealed record AppClockSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "App";

    /// <summary>
    /// Gets IANA time-zone id used to derive the app-local calendar day from UTC.
    /// Defaults to <c>America/New_York</c> when unconfigured.
    /// </summary>
    public string TimeZone { get; init; } = "America/New_York";
}
