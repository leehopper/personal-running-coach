namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Result of running <see cref="MesoMicroConsistencyValidator"/>. Carries the validity flag and the
/// violation kind only — no PII, no plan detail. Construct via <see cref="Valid"/> / <see cref="Invalid"/>
/// so a valid-with-violation contradiction cannot be expressed. Mirrors
/// <see cref="MacroPlanOutputValidationResult"/>; the concrete mismatch is recomputed by the
/// service's correction builder from the meso/micro outputs, so this result stays a pure discriminator.
/// </summary>
public sealed record MesoMicroConsistencyResult
{
    /// <summary>
    /// Constructs a result directly from its two component fields. Internal so production callers
    /// route through <see cref="Valid"/> / <see cref="Invalid"/>; the test assembly retains access
    /// via <c>InternalsVisibleTo</c>.
    /// </summary>
    /// <param name="isValid">True when the micro week is consistent with the meso week-1 template.</param>
    /// <param name="violation">The kind of inconsistency detected.</param>
    internal MesoMicroConsistencyResult(bool isValid, MesoMicroConsistencyViolation violation)
    {
        IsValid = isValid;
        Violation = violation;
    }

    /// <summary>Gets a value indicating whether the micro week is consistent with the meso template.</summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the kind of inconsistency detected, or <see cref="MesoMicroConsistencyViolation.None"/>
    /// when consistent.
    /// </summary>
    public MesoMicroConsistencyViolation Violation { get; }

    /// <summary>Returns the canonical consistent result: <c>IsValid=true</c>, <c>Violation=None</c>.</summary>
    public static MesoMicroConsistencyResult Valid() =>
        new(isValid: true, violation: MesoMicroConsistencyViolation.None);

    /// <summary>Returns an inconsistent result for the given violation kind.</summary>
    /// <param name="violation">
    /// The violation kind. Must not be <see cref="MesoMicroConsistencyViolation.None"/> —
    /// a consistent-with-violation result is a contradiction the factory rejects.
    /// </param>
    public static MesoMicroConsistencyResult Invalid(MesoMicroConsistencyViolation violation)
    {
        if (violation == MesoMicroConsistencyViolation.None)
        {
            throw new ArgumentException(
                "Invalid() requires a non-None violation; use Valid() for the consistent case.",
                nameof(violation));
        }

        return new MesoMicroConsistencyResult(isValid: false, violation: violation);
    }
}
