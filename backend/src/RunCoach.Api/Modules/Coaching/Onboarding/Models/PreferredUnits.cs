namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// The runner's preferred distance units for plan rendering.
/// Values are explicitly numbered for stable Marten/JSON wire encoding.
/// </summary>
public enum PreferredUnits
{
    /// <summary>Kilometers (default for Slice 1).</summary>
    Kilometers = 0,

    /// <summary>Miles.</summary>
    Miles = 1,
}
