using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the Preferences topic. Captures the runner's plan-rendering preferences
/// and qualitative preferences for terrain and intensity tolerance.
/// </summary>
public sealed record PreferencesAnswer
{
    /// <summary>
    /// Gets the preferred distance units for plan rendering.
    /// </summary>
    [Description("Preferred distance units for plan rendering: Kilometers or Miles.")]
    public required PreferredUnits PreferredUnits { get; init; }

    /// <summary>
    /// Gets a value indicating whether the runner prefers trail running where possible.
    /// </summary>
    [Description("Whether the runner prefers trail running where possible.")]
    public required bool PreferTrail { get; init; }

    /// <summary>
    /// Gets a value indicating whether the runner is comfortable with structured high-intensity workouts.
    /// </summary>
    [Description("Whether the runner is comfortable with structured high-intensity workouts (intervals, threshold).")]
    public required bool ComfortableWithIntensity { get; init; }

    /// <summary>
    /// Gets the runner-supplied free-text description of any other preferences.
    /// </summary>
    [Description("Runner-supplied free-text description of any other preferences not captured above.")]
    public required string Description { get; init; }
}
