namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Triages one inbound runner message into a <see cref="MessageIntentOutput"/> via a fast
/// Pattern-B Haiku call (Slice 4B / DEC-085 D3): compose the classifier prompt, run the
/// structured call on the classifier model binding, and validate the slot-matches-discriminator
/// invariant. Determinism comes from constrained decoding (the byte-stable frozen schema) plus
/// the deterministic prompt — the SDK exposes no sampling temperature on current models.
/// </summary>
public interface IMessageIntentClassifier
{
    /// <summary>
    /// Classifies the runner's message.
    /// </summary>
    /// <param name="today">The runner's app-local date, for resolving relative dates in a logged workout.</param>
    /// <param name="userMessage">The runner's RAW chat message (sanitized inside the assembler).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The validated intent. A structurally-invalid LLM union is coerced to
    /// <see cref="MessageIntent.Ambiguous"/> (DEC-085 bias-to-ask, never guess). LLM call
    /// failures surface as the <see cref="CoachingLlmException"/> hierarchy (DEC-073) for the
    /// caller to translate into an error frame.
    /// </returns>
    Task<MessageIntentOutput> ClassifyAsync(DateOnly today, string userMessage, CancellationToken ct);
}
