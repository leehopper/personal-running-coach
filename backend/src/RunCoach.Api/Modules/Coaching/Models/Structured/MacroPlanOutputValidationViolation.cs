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
    /// An anchored horizon exists but <see cref="MacroPlanOutput.TotalWeeks"/> does not place
    /// race week inside the final phase's last week (within the ±1-week tolerance).
    /// </summary>
    HorizonMismatch = 2,
}
