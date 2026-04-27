namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Result of running <see cref="OnboardingTurnOutputValidator"/> on a parsed
/// <see cref="OnboardingTurnOutput"/>. PII-free — carries violation kind +
/// counts only.
/// </summary>
/// <param name="IsValid">True when the Pattern-B-Invariant holds.</param>
/// <param name="Violation">
/// The kind of invariant violation detected, or
/// <see cref="OnboardingTurnOutputValidationViolation.None"/> when valid.
/// </param>
/// <param name="NonNullSlotCount">
/// Count of non-null <c>Normalized*</c> slots seen on the extracted answer.
/// Should always be exactly one when valid.
/// </param>
public readonly record struct OnboardingTurnOutputValidationResult(
    bool IsValid,
    OnboardingTurnOutputValidationViolation Violation,
    int NonNullSlotCount);
