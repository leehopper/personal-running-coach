using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the TargetEvent topic. Captures the specific race or event the runner
/// is training for when PrimaryGoal is RaceTraining.
/// </summary>
public sealed record TargetEventAnswer
{
    /// <summary>
    /// Gets the name of the goal race or event (e.g. "Berlin Marathon", "Local 10K").
    /// </summary>
    [Description("Name of the goal race or event, such as 'Berlin Marathon' or 'Local 10K'.")]
    public required string EventName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target distance in kilometers for the event.
    /// </summary>
    [Description("Target distance in kilometers for the event.")]
    public required double DistanceKm { get; init; }

    /// <summary>
    /// Gets the target event date in ISO-8601 calendar form (yyyy-MM-dd).
    /// </summary>
    [Description("Target event date in ISO-8601 calendar form (yyyy-MM-dd).")]
    public required string EventDateIso { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional target finishing time as ISO-8601 duration (PT1H45M30S) when the runner has one.
    /// </summary>
    [Description("Optional target finishing time as ISO-8601 duration (e.g. PT1H45M30S). Null if the runner has no specific time goal.")]
    public required string? TargetFinishTimeIso { get; init; }
}
