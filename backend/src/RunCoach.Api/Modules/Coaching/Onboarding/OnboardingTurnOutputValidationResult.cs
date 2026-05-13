namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Result of running <see cref="OnboardingTurnOutputValidator"/> on a parsed
/// <see cref="OnboardingTurnOutput"/>. PII-free — carries violation kind +
/// counts only.
/// </summary>
/// <remarks>
/// The constructor is <c>internal</c>; external callers must construct via
/// <see cref="Valid"/> or <see cref="Invalid"/> so contradictory triples
/// (e.g. <c>IsValid: true</c> alongside a non-<c>None</c> violation) cannot be
/// expressed.
/// </remarks>
public sealed record OnboardingTurnOutputValidationResult
{
    /// <summary>
    /// Constructs a result directly from the three component fields. Internal
    /// so production callers must route through <see cref="Valid"/> and
    /// <see cref="Invalid"/>; the test assembly retains access via
    /// <c>InternalsVisibleTo</c>.
    /// </summary>
    /// <param name="isValid">True when the Pattern-B-Invariant holds.</param>
    /// <param name="violation">The kind of invariant violation detected.</param>
    /// <param name="nonNullSlotCount">Count of non-null <c>Normalized*</c> slots.</param>
    internal OnboardingTurnOutputValidationResult(
        bool isValid,
        OnboardingTurnOutputValidationViolation violation,
        int nonNullSlotCount)
    {
        IsValid = isValid;
        Violation = violation;
        NonNullSlotCount = nonNullSlotCount;
    }

    /// <summary>Gets a value indicating whether true when the Pattern-B-Invariant holds.</summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the kind of invariant violation detected, or
    /// <see cref="OnboardingTurnOutputValidationViolation.None"/> when valid.
    /// </summary>
    public OnboardingTurnOutputValidationViolation Violation { get; init; }

    /// <summary>
    /// Gets count of non-null <c>Normalized*</c> slots seen on the extracted answer.
    /// Should always be exactly one when valid.
    /// </summary>
    public int NonNullSlotCount { get; init; }

    /// <summary>
    /// Returns the canonical valid result: <c>IsValid=true</c>,
    /// <c>Violation=None</c>, <c>NonNullSlotCount=nonNullSlotCount</c>.
    /// </summary>
    /// <param name="nonNullSlotCount">
    /// Count of non-null <c>Normalized*</c> slots on the extracted answer.
    /// Zero for the vacuous case (<c>Extracted</c> is null); one for the
    /// well-formed case.
    /// </param>
    public static OnboardingTurnOutputValidationResult Valid(int nonNullSlotCount = 0)
    {
        if (nonNullSlotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nonNullSlotCount), "nonNullSlotCount must be non-negative.");
        }

        return new(isValid: true, violation: OnboardingTurnOutputValidationViolation.None, nonNullSlotCount: nonNullSlotCount);
    }

    /// <summary>
    /// Returns an invalid result for the given violation kind.
    /// </summary>
    /// <param name="violation">
    /// The violation kind. Must not be <see cref="OnboardingTurnOutputValidationViolation.None"/> —
    /// a valid-with-violation result is a contradiction the factory rejects.
    /// </param>
    /// <param name="nonNullSlotCount">
    /// Count of non-null <c>Normalized*</c> slots on the extracted answer, if
    /// any. Defaults to zero for shape-level violations where slots were
    /// never inspected.
    /// </param>
    public static OnboardingTurnOutputValidationResult Invalid(
        OnboardingTurnOutputValidationViolation violation,
        int nonNullSlotCount = 0)
    {
        if (nonNullSlotCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nonNullSlotCount), "nonNullSlotCount must be non-negative.");
        }

        if (violation == OnboardingTurnOutputValidationViolation.None)
        {
            throw new ArgumentException(
                "Invalid() requires a non-None violation; use Valid() for the well-formed case.",
                nameof(violation));
        }

        return new OnboardingTurnOutputValidationResult(
            isValid: false,
            violation: violation,
            nonNullSlotCount: nonNullSlotCount);
    }
}
