namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The result of validating a <see cref="MessageIntentOutput"/> against the
/// Pattern-B invariants Anthropic constrained decoding cannot express. Construct
/// only via <see cref="Valid"/> / <see cref="Invalid"/> so a contradictory state
/// (valid with a non-None violation) is unrepresentable. Mirrors
/// <c>PlanAdaptationOutputValidationResult</c>.
/// </summary>
public sealed record MessageIntentOutputValidationResult
{
    private MessageIntentOutputValidationResult(bool isValid, MessageIntentOutputValidationViolation violation)
    {
        IsValid = isValid;
        Violation = violation;
    }

    /// <summary>Gets a value indicating whether the output satisfies every invariant.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the first invariant violated, or <see cref="MessageIntentOutputValidationViolation.None"/> when valid.</summary>
    public MessageIntentOutputValidationViolation Violation { get; }

    /// <summary>Creates a valid result.</summary>
    /// <returns>A result with <see cref="IsValid"/> true and no violation.</returns>
    public static MessageIntentOutputValidationResult Valid() =>
        new(isValid: true, violation: MessageIntentOutputValidationViolation.None);

    /// <summary>Creates an invalid result carrying the violated invariant.</summary>
    /// <param name="violation">The violated invariant; must not be <see cref="MessageIntentOutputValidationViolation.None"/>.</param>
    /// <returns>A result with <see cref="IsValid"/> false and the given violation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="violation"/> is <see cref="MessageIntentOutputValidationViolation.None"/>.</exception>
    public static MessageIntentOutputValidationResult Invalid(MessageIntentOutputValidationViolation violation)
    {
        if (violation == MessageIntentOutputValidationViolation.None)
        {
            throw new ArgumentException(
                "Invalid() requires a non-None violation; use Valid() for the well-formed case.",
                nameof(violation));
        }

        return new MessageIntentOutputValidationResult(isValid: false, violation: violation);
    }
}
