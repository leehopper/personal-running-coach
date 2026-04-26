using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Normalized answer for the InjuryHistory topic. Captures whether the runner currently has
/// active limitations and free-text descriptions of past and current issues.
/// </summary>
public sealed record InjuryHistoryAnswer
{
    /// <summary>
    /// Gets a value indicating whether the runner currently has an active injury or pain
    /// that limits training.
    /// </summary>
    [Description("Whether the runner currently has an active injury or pain that limits training.")]
    public required bool HasActiveInjury { get; init; }

    /// <summary>
    /// Gets the runner-supplied description of the active injury or limitation, when present.
    /// </summary>
    [Description("Description of the active injury or limitation. Empty string when there is no active injury.")]
    public required string ActiveInjuryDescription { get; init; }

    /// <summary>
    /// Gets the runner-supplied summary of past injuries or recurring issues that should
    /// inform the training plan.
    /// </summary>
    [Description("Summary of past injuries or recurring issues that should inform the training plan. Empty string if none.")]
    public required string PastInjurySummary { get; init; }
}
