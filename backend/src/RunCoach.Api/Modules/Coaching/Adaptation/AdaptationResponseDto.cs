namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Flat response envelope for the synchronous adaptation flow (DEC-073). The log-create itself
/// always commits, so a terminal coaching-LLM failure is surfaced as a <c>Kind=Error</c> envelope
/// over HTTP 200 — never a 5xx — carrying flat <see cref="ErrorMessage"/> / <see cref="Retryable"/>
/// / <see cref="RetryAfterSeconds"/> fields the frontend reads without any SDK dependency.
/// </summary>
/// <remarks>
/// The success-path payload is deliberately lean: <see cref="Kind"/> plus the resolved
/// <see cref="AdaptationKind"/>. Panel data (the adaptation turn, the before/after diff) flows
/// through the plan + conversation read models, which the frontend refetches by invalidating its
/// query tags after every successful log create — no create-response coupling (Slice 3 § Unit 7).
/// </remarks>
public sealed record AdaptationResponseDto
{
    /// <summary>Gets the discriminator: <see cref="AdaptationResponseKind.Adapted"/> or <see cref="AdaptationResponseKind.Error"/>.</summary>
    public required AdaptationResponseKind Kind { get; init; }

    /// <summary>
    /// Gets the resolved plan-change kind when <see cref="Kind"/> is
    /// <see cref="AdaptationResponseKind.Adapted"/>; null on <see cref="AdaptationResponseKind.Error"/>.
    /// <see cref="Training.Adaptation.AdaptationKind.Absorb"/> covers every no-plan-change outcome
    /// (off-plan, on-target, and the Red safety short-circuit — the safety turn itself surfaces via
    /// the conversation read model).
    /// </summary>
    public Training.Adaptation.AdaptationKind? AdaptationKind { get; init; }

    /// <summary>
    /// Gets the user-facing error message when <see cref="Kind"/> is
    /// <see cref="AdaptationResponseKind.Error"/>; null otherwise.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a value indicating whether the failure is retryable (true for a transient failure,
    /// false for a permanent one). The frontend does not auto-retry at MVP-0; this is an
    /// informational hint.
    /// </summary>
    public bool Retryable { get; init; }

    /// <summary>
    /// Gets the server-advised retry delay in seconds for a retryable failure, when one was
    /// advertised via <c>Retry-After</c>; null otherwise.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Builds the <c>Kind=Adapted</c> success envelope for the given resolved plan-change kind.
    /// </summary>
    /// <param name="adaptationKind">The plan-change kind the evaluation resolved.</param>
    /// <returns>A <c>Kind=Adapted</c> envelope.</returns>
    public static AdaptationResponseDto Adapted(Training.Adaptation.AdaptationKind adaptationKind) =>
        new()
        {
            Kind = AdaptationResponseKind.Adapted,
            AdaptationKind = adaptationKind,
        };

    /// <summary>
    /// Builds the <c>Kind=Error</c> envelope from a terminal <see cref="CoachingLlmException"/>:
    /// a <see cref="TransientCoachingLlmException"/> yields a retryable envelope carrying its
    /// retry-after hint; a <see cref="PermanentCoachingLlmException"/> yields a non-retryable one.
    /// </summary>
    /// <param name="exception">The terminal coaching-LLM failure.</param>
    /// <returns>A <c>Kind=Error</c> envelope.</returns>
    public static AdaptationResponseDto FromError(CoachingLlmException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var transient = exception as TransientCoachingLlmException;

        return new AdaptationResponseDto
        {
            Kind = AdaptationResponseKind.Error,
            ErrorMessage = exception.Message,
            Retryable = transient is not null,
            RetryAfterSeconds = transient?.RetryAfterSeconds,
        };
    }
}
