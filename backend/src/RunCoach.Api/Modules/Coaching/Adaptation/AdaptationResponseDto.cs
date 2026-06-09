namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Flat response envelope for the synchronous adaptation flow (DEC-073). The log-create itself
/// always commits, so a terminal coaching-LLM failure is surfaced as a <c>Kind=Error</c> envelope
/// over HTTP 200 — never a 5xx — carrying flat <see cref="ErrorMessage"/> / <see cref="Retryable"/>
/// / <see cref="RetryAfterSeconds"/> fields the frontend reads without any SDK dependency.
/// </summary>
/// <remarks>
/// This type carries the error shape and its derivation from a
/// <see cref="CoachingLlmException"/>; the success-path payload (the adaptation turn / plan
/// re-render signal) is owned by the orchestration layer and is outside this type's scope.
/// </remarks>
public sealed record AdaptationResponseDto
{
    /// <summary>Gets the discriminator: <see cref="AdaptationResponseKind.Adapted"/> or <see cref="AdaptationResponseKind.Error"/>.</summary>
    public required AdaptationResponseKind Kind { get; init; }

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
