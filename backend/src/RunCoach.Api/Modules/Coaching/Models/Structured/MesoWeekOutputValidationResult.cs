namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Result of running <see cref="MesoWeekOutputValidator"/>. Carries the validity flag and
/// the violation kind only — no PII. Construct via <see cref="Valid"/> / <see cref="Invalid"/>
/// so a valid-with-violation contradiction cannot be expressed. Mirrors
/// <see cref="MacroPlanOutputValidationResult"/>.
/// </summary>
public sealed record MesoWeekOutputValidationResult
{
    /// <summary>
    /// Constructs a result directly from its two component fields. Internal so production
    /// callers route through <see cref="Valid"/> / <see cref="Invalid"/>; the test
    /// assembly retains access via <c>InternalsVisibleTo</c>.
    /// </summary>
    /// <param name="isValid">True when every invariant holds.</param>
    /// <param name="violation">The kind of invariant violation detected.</param>
    internal MesoWeekOutputValidationResult(
        bool isValid,
        MesoWeekOutputValidationViolation violation)
    {
        IsValid = isValid;
        Violation = violation;
    }

    /// <summary>Gets a value indicating whether every invariant holds.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the kind of invariant violation detected, or
    /// <see cref="MesoWeekOutputValidationViolation.None"/> when valid.
    /// </summary>
    public MesoWeekOutputValidationViolation Violation { get; }

    /// <summary>
    /// Returns the canonical valid result: <c>IsValid=true</c>, <c>Violation=None</c>.
    /// </summary>
    public static MesoWeekOutputValidationResult Valid() =>
        new(isValid: true, violation: MesoWeekOutputValidationViolation.None);

    /// <summary>
    /// Returns an invalid result for the given violation kind.
    /// </summary>
    /// <param name="violation">
    /// The violation kind. Must not be <see cref="MesoWeekOutputValidationViolation.None"/> —
    /// a valid-with-violation result is a contradiction the factory rejects.
    /// </param>
    public static MesoWeekOutputValidationResult Invalid(MesoWeekOutputValidationViolation violation)
    {
        if (violation == MesoWeekOutputValidationViolation.None)
        {
            throw new ArgumentException(
                "Invalid() requires a non-None violation; use Valid() for the well-formed case.",
                nameof(violation));
        }

        return new MesoWeekOutputValidationResult(isValid: false, violation: violation);
    }
}
