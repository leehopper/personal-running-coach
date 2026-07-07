namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Result of running <see cref="PlanAdaptationOutputValidator"/> on a parsed
/// <see cref="PlanAdaptationOutput"/>. Carries the validity flag and the violation kind
/// only — no PII.
/// </summary>
/// <remarks>
/// The constructor is <c>internal</c>; callers construct via <see cref="Valid"/> or
/// <see cref="Invalid"/> so contradictory pairs (e.g. <c>IsValid: true</c> with a
/// non-<c>None</c> violation) cannot be expressed. Mirrors <c>SafetyClassification</c>.
/// </remarks>
public sealed record PlanAdaptationOutputValidationResult
{
    /// <summary>
    /// Constructs a result directly from its two component fields. Internal so production
    /// callers route through <see cref="Valid"/> / <see cref="Invalid"/>; the test
    /// assembly retains access via <c>InternalsVisibleTo</c>.
    /// </summary>
    /// <param name="isValid">True when every invariant holds.</param>
    /// <param name="violation">The kind of invariant violation detected.</param>
    internal PlanAdaptationOutputValidationResult(
        bool isValid,
        PlanAdaptationOutputValidationViolation violation)
    {
        IsValid = isValid;
        Violation = violation;
    }

    /// <summary>Gets a value indicating whether every invariant holds.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the kind of invariant violation detected, or
    /// <see cref="PlanAdaptationOutputValidationViolation.None"/> when valid.
    /// </summary>
    public PlanAdaptationOutputValidationViolation Violation { get; }

    /// <summary>
    /// Returns the canonical valid result: <c>IsValid=true</c>, <c>Violation=None</c>.
    /// </summary>
    public static PlanAdaptationOutputValidationResult Valid() =>
        new(isValid: true, violation: PlanAdaptationOutputValidationViolation.None);

    /// <summary>
    /// Returns an invalid result for the given violation kind.
    /// </summary>
    /// <param name="violation">
    /// The violation kind. Must not be <see cref="PlanAdaptationOutputValidationViolation.None"/> —
    /// a valid-with-violation result is a contradiction the factory rejects.
    /// </param>
    public static PlanAdaptationOutputValidationResult Invalid(
        PlanAdaptationOutputValidationViolation violation)
    {
        if (violation == PlanAdaptationOutputValidationViolation.None)
        {
            throw new ArgumentException(
                "Invalid() requires a non-None violation; use Valid() for the well-formed case.",
                nameof(violation));
        }

        return new PlanAdaptationOutputValidationResult(isValid: false, violation: violation);
    }
}
