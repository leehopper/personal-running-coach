using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Production <see cref="IMessageIntentClassifier"/>: composes the classifier prompt via
/// <see cref="IContextAssembler.ComposeForClassificationAsync"/>, runs the Pattern-B structured
/// call on the Haiku classifier binding (per-call model override; no temperature — SDK-deprecated,
/// DEC-085 § PR3a correction), and enforces the slot-matches-discriminator invariant. A
/// validator reject is coerced to <see cref="MessageIntent.Ambiguous"/> (DEC-085 bias-to-ask).
/// </summary>
public sealed partial class MessageIntentClassifier(
    IContextAssembler contextAssembler,
    ICoachingLlm coachingLlm,
    IOptions<CoachingLlmSettings> settings,
    ILogger<MessageIntentClassifier> logger) : IMessageIntentClassifier
{
    private readonly IContextAssembler _contextAssembler = contextAssembler;
    private readonly ICoachingLlm _coachingLlm = coachingLlm;
    private readonly CoachingLlmSettings _settings = settings.Value;
    private readonly ILogger<MessageIntentClassifier> _logger = logger;

    /// <inheritdoc />
    public async Task<MessageIntentOutput> ClassifyAsync(DateOnly today, string userMessage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        var composition = await _contextAssembler
            .ComposeForClassificationAsync(today, userMessage, ct)
            .ConfigureAwait(false);

        // Constrained decoding on the byte-stable frozen schema; targets the Haiku classifier
        // binding via the per-call model override. The system prompt is small and re-run per
        // message, so it is not prompt-cached.
        var (output, _) = await _coachingLlm
            .GenerateStructuredAsync<MessageIntentOutput>(
                composition.SystemPrompt,
                composition.UserMessage,
                ClassifierSchema.Frozen,
                cacheControl: null,
                modelOverride: _settings.ClassifierModelId,
                ct)
            .ConfigureAwait(false);

        var validation = MessageIntentOutputValidator.Validate(output);
        if (!validation.IsValid)
        {
            // A structurally-invalid union is low-confidence: ask rather than guess
            // (DEC-085). The card already absorbs a parse miss via its Edit affordance.
            LogClassifierValidatorRejected(_logger, validation.Violation);
            return new MessageIntentOutput { Intent = MessageIntent.Ambiguous, WorkoutLog = null };
        }

        return output;
    }

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Warning,
        Message = "Intent classifier output failed validation ({Violation}); coercing to Ambiguous.")]
    private static partial void LogClassifierValidatorRejected(
        ILogger logger,
        MessageIntentOutputValidationViolation violation);
}
