namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Post-deserialization validator that enforces the Pattern-B-Invariant per
/// R-067 / DEC-058: when <see cref="OnboardingTurnOutput.Extracted"/> is
/// non-null, exactly one of the six <c>Normalized*</c> slots is non-null AND
/// it matches <see cref="ExtractedAnswer.Topic"/>. Anthropic constrained
/// decoding cannot express this invariant (it rejects <c>oneOf</c>) so the
/// backend enforces it at the .NET boundary.
/// </summary>
/// <remarks>
/// <para>
/// On failure the onboarding turn handler retries the turn once with a
/// stronger discriminator-instruction in the user message. On second failure
/// it emits <c>ClarificationRequested(Topic, "Discriminator mismatch")</c>
/// per spec § Unit 1 R01.5. The validator itself is pure: it does not
/// retry, log, or throw — callers decide policy.
/// </para>
/// <para>
/// When <see cref="OnboardingTurnOutput.Extracted"/> is null the invariant is
/// vacuously satisfied (the LLM correctly reported no extraction was made,
/// e.g. because the runner asked their own clarifying question). The
/// validator returns valid in that case regardless of slot state — slot
/// inspection only runs when <c>Extracted</c> is non-null.
/// </para>
/// </remarks>
public static class OnboardingTurnOutputValidator
{
    /// <summary>
    /// Single source of truth mapping each <see cref="OnboardingTopic"/> to the
    /// <see cref="ExtractedAnswer"/> slot that carries its normalized data.
    /// Both <see cref="CountNonNullSlots"/> and <see cref="SlotMatchesTopic"/>
    /// are derived from this map, ensuring the topic→slot relationship is
    /// defined exactly once.
    /// </summary>
    private static readonly Dictionary<OnboardingTopic, Func<ExtractedAnswer, object?>> TopicSlotAccessors =
        new()
        {
            [OnboardingTopic.PrimaryGoal] = e => e.NormalizedPrimaryGoal,
            [OnboardingTopic.TargetEvent] = e => e.NormalizedTargetEvent,
            [OnboardingTopic.CurrentFitness] = e => e.NormalizedCurrentFitness,
            [OnboardingTopic.WeeklySchedule] = e => e.NormalizedWeeklySchedule,
            [OnboardingTopic.InjuryHistory] = e => e.NormalizedInjuryHistory,
            [OnboardingTopic.Preferences] = e => e.NormalizedPreferences,
        };

    /// <summary>
    /// Validates the Pattern-B-Invariant against a deserialized turn output.
    /// </summary>
    /// <param name="output">The deserialized LLM turn output.</param>
    /// <param name="currentTopic">
    /// The topic the assistant was asked to address on this turn — used to
    /// detect Topic-discriminator drift when <c>Extracted.Topic</c> disagrees
    /// with the active topic. Pass the topic resolved by the deterministic
    /// next-topic selector.
    /// </param>
    /// <returns>The validation result.</returns>
    public static OnboardingTurnOutputValidationResult Validate(
        OnboardingTurnOutput output,
        OnboardingTopic currentTopic)
    {
        ArgumentNullException.ThrowIfNull(output);

        // Clarification consistency check is independent of Extracted.
        if (output.NeedsClarification && string.IsNullOrWhiteSpace(output.ClarificationReason))
        {
            return new OnboardingTurnOutputValidationResult(
                IsValid: false,
                Violation: OnboardingTurnOutputValidationViolation.ClarificationWithoutReason,
                NonNullSlotCount: 0);
        }

        // No extraction reported: invariant is vacuously satisfied. The LLM
        // chose to ask a clarifying question or otherwise not commit to a
        // normalized answer this turn.
        if (output.Extracted is null)
        {
            return new OnboardingTurnOutputValidationResult(
                IsValid: true,
                Violation: OnboardingTurnOutputValidationViolation.None,
                NonNullSlotCount: 0);
        }

        var extracted = output.Extracted;
        var nonNullCount = CountNonNullSlots(extracted);

        if (nonNullCount == 0)
        {
            return new OnboardingTurnOutputValidationResult(
                IsValid: false,
                Violation: OnboardingTurnOutputValidationViolation.NoNormalizedSlot,
                NonNullSlotCount: 0);
        }

        if (nonNullCount > 1)
        {
            return new OnboardingTurnOutputValidationResult(
                IsValid: false,
                Violation: OnboardingTurnOutputValidationViolation.MultipleNormalizedSlots,
                NonNullSlotCount: nonNullCount);
        }

        if (!SlotMatchesTopic(extracted, extracted.Topic))
        {
            return new OnboardingTurnOutputValidationResult(
                IsValid: false,
                Violation: OnboardingTurnOutputValidationViolation.SlotTopicMismatch,
                NonNullSlotCount: nonNullCount);
        }

        // Currently `currentTopic` is informational — it is not enforced as
        // strictly equal to `Extracted.Topic` because the LLM may legitimately
        // capture a topic out of canonical order (e.g. the runner volunteered
        // injury info while answering a fitness question). Future hardening
        // can promote this to an error per R-067-T1.
        _ = currentTopic;

        return new OnboardingTurnOutputValidationResult(
            IsValid: true,
            Violation: OnboardingTurnOutputValidationViolation.None,
            NonNullSlotCount: nonNullCount);
    }

    private static int CountNonNullSlots(ExtractedAnswer extracted) =>
        TopicSlotAccessors.Values.Count(accessor => accessor(extracted) is not null);

    private static bool SlotMatchesTopic(ExtractedAnswer extracted, OnboardingTopic topic) =>
        TopicSlotAccessors.TryGetValue(topic, out var accessor) && accessor(extracted) is not null;
}
