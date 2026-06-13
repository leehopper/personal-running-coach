namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Kinds of invariant violation <see cref="MacroPlanOutputValidator"/> can detect on a
/// deserialized <see cref="MacroPlanOutput"/>. Mirrors the adaptation violation enum.
/// </summary>
public enum MacroPlanOutputValidationViolation
{
    /// <summary>No violation — the macro is valid.</summary>
    None = 0,

    /// <summary>The phase week counts do not sum to <see cref="MacroPlanOutput.TotalWeeks"/>.</summary>
    PhaseSumMismatch = 1,

    /// <summary>
    /// An anchored horizon exists but <see cref="MacroPlanOutput.TotalWeeks"/> deviates from the
    /// required total weeks (the race-week index) by more than the ±1-week tolerance. Because the
    /// phase-sum check runs first, TotalWeeks equals the final phase's last week, so this places
    /// race week outside the taper's final week.
    /// </summary>
    HorizonMismatch = 2,
}
