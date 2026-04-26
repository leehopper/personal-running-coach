namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// The frontend input control the chat surface should render for the current question.
/// Mirrors the discriminated-union component map per R-065.
/// </summary>
public enum SuggestedInputType
{
    /// <summary>Free-text input.</summary>
    Text = 0,

    /// <summary>Single-select from a fixed option list.</summary>
    SingleSelect = 1,

    /// <summary>Multi-select from a fixed option list (e.g. weekly run-day flags).</summary>
    MultiSelect = 2,

    /// <summary>Numeric input (e.g. weekly distance).</summary>
    Numeric = 3,

    /// <summary>Calendar date input.</summary>
    Date = 4,
}
