namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Pattern-B-Invariant violation taxonomy. Stable enum values — never reorder.
/// </summary>
public enum OnboardingTurnOutputValidationViolation
{
    /// <summary>The invariant holds — output is valid.</summary>
    None = 0,

    /// <summary>Zero <c>Normalized*</c> slots are non-null.</summary>
    NoNormalizedSlot = 1,

    /// <summary>More than one <c>Normalized*</c> slot is non-null.</summary>
    MultipleNormalizedSlots = 2,

    /// <summary>The non-null slot does not match <c>Extracted.Topic</c>.</summary>
    SlotTopicMismatch = 3,

    /// <summary>
    /// <see cref="OnboardingTurnOutput.NeedsClarification"/> is true but
    /// <see cref="OnboardingTurnOutput.ClarificationReason"/> is null/empty.
    /// </summary>
    ClarificationWithoutReason = 4,
}
