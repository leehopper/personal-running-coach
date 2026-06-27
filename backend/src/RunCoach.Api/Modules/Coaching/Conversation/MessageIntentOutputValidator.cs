namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Enforces the Pattern-B invariant Anthropic constrained decoding cannot express for
/// <see cref="MessageIntentOutput"/>: the populated slot matches the discriminator
/// (exactly the <see cref="MessageIntent.WorkoutLog"/> intent fills the
/// <see cref="MessageIntentOutput.WorkoutLog"/> slot). Mirrors
/// <c>OnboardingTurnOutputValidator</c> / <c>PlanAdaptationOutputValidator</c>. Pure:
/// it does not retry, log, or throw (beyond the null guard) — callers decide policy
/// (DEC-085 biases a failed/low-confidence classify toward asking, not guessing).
/// </summary>
public static class MessageIntentOutputValidator
{
    /// <summary>Validates a deserialized <see cref="MessageIntentOutput"/>.</summary>
    /// <param name="output">The deserialized classifier output.</param>
    /// <returns>A <see cref="MessageIntentOutputValidationResult"/> describing the first violated invariant, if any.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="output"/> is null.</exception>
    public static MessageIntentOutputValidationResult Validate(MessageIntentOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        // The populated slot (or its absence) must match the discriminator: only the
        // WorkoutLog intent carries a draft. The default arm fails closed so an
        // out-of-range intent (JsonStringEnumConverter accepts integer values) is rejected.
        var hasDraft = output.WorkoutLog is not null;
        var slotMatchesIntent = output.Intent switch
        {
            MessageIntent.Question or MessageIntent.Ambiguous => !hasDraft,
            MessageIntent.WorkoutLog => hasDraft,
            _ => false,
        };

        if (!slotMatchesIntent)
        {
            return MessageIntentOutputValidationResult.Invalid(
                MessageIntentOutputValidationViolation.SlotIntentMismatch);
        }

        return MessageIntentOutputValidationResult.Valid();
    }
}
