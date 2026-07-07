namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Strongly-typed settings for the coaching LLM adapter.
/// Mapped from the "Anthropic" configuration section.
/// </summary>
public sealed record CoachingLlmSettings
{
    /// <summary>
    /// The configuration section name in appsettings/user-secrets.
    /// </summary>
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Gets the Anthropic API key. Must be provided via user-secrets or
    /// environment variables — never committed to source.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Claude model identifier for coaching tasks.
    /// Uses a floating alias by default for automatic upgrades within the family.
    /// Override with a dated ID (e.g., "claude-sonnet-4-6-20260101") for pinned evals.
    /// </summary>
    public string ModelId { get; init; } = "claude-sonnet-4-6";

    /// <summary>
    /// Gets maximum tokens for the response.
    /// Defaults to 4096 per coaching-v1.yaml.
    /// </summary>
    public int MaxTokens { get; init; } = 8192;

    /// <summary>
    /// Gets the maximum number of retries the Anthropic SDK makes for failed requests (rate
    /// limits, transient 5xx/network errors), with exponential backoff that honors
    /// <c>Retry-After</c>. <c>MaxRetries = N</c> means up to <c>N + 1</c> attempts. Defaults to
    /// 2 (3 attempts) per DEC-073, matching the SDK's own default.
    /// </summary>
    public int MaxRetries { get; init; } = 2;

    /// <summary>
    /// Gets the maximum number of times the plan-generation macro tier is re-invoked when its
    /// deterministic output validator (<c>MacroPlanOutputValidator</c>) rejects the generated macro
    /// (DEC-087). <c>MacroValidationMaxRetries = N</c> means up to <c>N + 1</c> macro attempts before
    /// the generation is terminally rejected — mirroring <see cref="MaxRetries"/>'s own semantics.
    /// Distinct from <see cref="MaxRetries"/> (SDK transport/rate-limit retries): this governs a
    /// bad-output re-roll of a well-formed-but-invalid macro, with no backoff, and each retry carries
    /// a deterministic corrective hint naming the arithmetic the model got wrong. Defaults to 1
    /// (2 attempts); raise it to trade worst-case plan-generation latency for resilience against
    /// back-to-back stochastic rejections without a redeploy.
    /// </summary>
    public int MacroValidationMaxRetries { get; init; } = 1;

    /// <summary>
    /// Gets the maximum number of times the plan-generation micro tier is re-invoked when its
    /// deterministic cross-layer validator (<c>MesoMicroConsistencyValidator</c>) rejects the
    /// generated week-1 workouts for disagreeing with the meso week-1 template — a differing run-day
    /// count or a swapped workout type (DEC-088 / F-LIVE-2). <c>MicroValidationMaxRetries = N</c>
    /// means up to <c>N + 1</c> micro attempts before the generation is terminally rejected —
    /// mirroring <see cref="MacroValidationMaxRetries"/>'s own semantics. The micro tier is the last
    /// of the six LLM calls and the meso week is the already-paid-for ground truth, so a re-roll
    /// costs exactly one micro call and carries a deterministic corrective hint naming the run-day
    /// schedule the model must reproduce. No backoff. Defaults to 1 (2 attempts); raise it to trade
    /// worst-case plan-generation latency for resilience against back-to-back stochastic
    /// inconsistencies without a redeploy.
    /// </summary>
    public int MicroValidationMaxRetries { get; init; } = 1;

    /// <summary>
    /// Gets the SDK per-attempt request timeout in seconds. Defaults to 120 seconds (2 minutes):
    /// this single setting governs every coaching call, and the plan-generation structured-output
    /// responses routinely need far longer than the ~30s DEC-073 sketched for the adaptation call
    /// alone — the shared 120s bound deliberately supersedes that figure rather than starving plan
    /// generation. A timed-out attempt surfaces as a <see cref="TransientCoachingLlmException"/>.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Gets the model identifier for LLM-as-judge calls (safety rubric evaluation).
    /// Uses Haiku 4.5 floating alias for cost-effective judging (~$0.0015/eval).
    /// </summary>
    public string JudgeModelId { get; init; } = "claude-haiku-4-5";

    /// <summary>
    /// Gets the model identifier for the interactive-conversation intent classifier
    /// (Slice 4B, DEC-085 D3). Uses the Haiku 4.5 floating alias (DEC-037) for a fast,
    /// cheap triage call, passed per-call via the <c>GenerateStructuredAsync</c> model
    /// override (PR3a) so the default coaching model is untouched. Distinct from
    /// <see cref="JudgeModelId"/> (eval-only) so production classifier behavior is not
    /// coupled to a test-only setting.
    /// </summary>
    public string ClassifierModelId { get; init; } = "claude-haiku-4-5";
}
