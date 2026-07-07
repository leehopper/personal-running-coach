using RunCoach.Api.Modules.Coaching.Conversation;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Plan;
using RunCoach.Api.Modules.Training.Plan.Models;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Assembles the full prompt payload from user data, enforcing positional
/// layout and token budget. Loads system prompts from versioned YAML files
/// via <see cref="Prompts.IPromptStore"/> and renders context templates
/// with <see cref="Prompts.PromptRenderer"/>.
///
/// The assembled prompt is split into a static prefix (coaching persona,
/// safety rules, semantic output guidance) suitable for Anthropic prompt
/// caching, and a dynamic suffix (rendered athlete context, conversation
/// history) that changes per request.
/// </summary>
public interface IContextAssembler
{
    /// <summary>
    /// Builds the full prompt payload from the provided input data.
    /// Loads the system prompt from the configured YAML prompt store.
    /// Applies positional layout (stable prefix, variable middle, conversational end)
    /// and enforces the token budget by truncating/summarizing as needed.
    /// </summary>
    /// <param name="input">All input data for prompt assembly.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The assembled prompt with structured sections and token estimate.</returns>
    Task<AssembledPrompt> AssembleAsync(ContextAssemblerInput input, CancellationToken ct = default);

    /// <summary>
    /// Composes the stable-prefix prompt for the macro/meso/micro plan-generation
    /// chain (Slice 1 § Unit 2 R02.4 + § Unit 5 R05.4). Reads the captured
    /// profile from the <paramref name="profileSnapshot"/> projection — NOT by
    /// replaying the onboarding stream — and renders it into a byte-stable user
    /// message. When <paramref name="intent"/> is supplied, its sanitized
    /// free-text is appended at the END of the user message under the stable
    /// label <c>[Regeneration intent provided by user]</c> so the prefix above
    /// it stays byte-identical across initial-generation and regenerate calls.
    /// </summary>
    /// <param name="profileSnapshot">
    /// The completed <see cref="OnboardingView"/> projection — the captured
    /// answers across the six topics serve as the profile snapshot for plan
    /// generation. Per Slice 1 spec the snapshot is read directly; the chain
    /// does NOT re-replay the onboarding event stream on each macro/meso/micro
    /// call.
    /// </param>
    /// <param name="intent">
    /// Optional regeneration intent supplied by the runner when invoking
    /// Settings → Plan (Slice 1 § Unit 5). The free-text MUST already be
    /// sanitized by the caller via
    /// <c>IPromptSanitizer.SanitizeAsync(intent.FreeText, PromptSection.RegenerationIntentFreeText, ct)</c>.
    /// This method does NOT re-sanitize.
    /// </param>
    /// <param name="today">
    /// The app-local calendar "today" (F3). Always emitted in the PLAN DATE CONTEXT block so the
    /// generated plan is date-aware, regardless of whether the horizon anchors to an event.
    /// </param>
    /// <param name="horizon">
    /// The deterministic <see cref="PlanHorizon"/> computed for this generation (F3). When anchored,
    /// the PLAN DATE CONTEXT block adds a hard event-anchoring instruction pinning the total weeks,
    /// naming race week, and requiring the final (taper) phase to end on race week; when not anchored,
    /// only the current date is emitted (general-fitness behavior).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed plan-generation prompt — system + base user message.</returns>
    Task<PlanGenerationPromptComposition> ComposeForPlanGenerationAsync(
        OnboardingView profileSnapshot,
        RegenerationIntent? intent,
        DateOnly today,
        PlanHorizon horizon,
        CancellationToken ct = default);

    /// <summary>
    /// Composes the system prompt + user message for a Level-2 adaptation
    /// (restructure) call (Slice 3 § Unit 5 / DEC-012). The system prompt is
    /// loaded from the versioned adaptation prompt YAML and is byte-stable
    /// per version (cacheable prefix — the calling service pairs it with
    /// prompt caching at call time). The user message renders the adaptation
    /// context template tokens: plan context, the deterministic escalation
    /// level / safety tier / deviation summary (echo-back only — the LLM
    /// never computes deviation math or re-classifies safety), and the
    /// triggering logged workout inside a nonce-delimited spotlight section
    /// per R-068 / DEC-059. At MVP-0 the runner-profile block is deliberately
    /// omitted (DEC-080): the composition renders plan context, escalation level,
    /// safety tier, deviation summary, and the triggering log only.
    /// </summary>
    /// <param name="plan">
    /// The current plan projection. The current micro week's detailed
    /// workouts and the meso weekly volume targets are rendered as plan
    /// context (week 1 is the only week carrying daily detail at MVP-0).
    /// </param>
    /// <param name="escalationLevel">
    /// The DEC-012 ladder level the deterministic escalation classifier
    /// resolved before this call.
    /// </param>
    /// <param name="safetyTier">
    /// The safety tier the deterministic safety gate resolved before this
    /// call. Rendered for echo-back only — the prompt instructs the LLM to
    /// echo it, never to re-decide it.
    /// </param>
    /// <param name="deviation">
    /// The deterministic deviation measurement; its numbers are rendered
    /// verbatim so the LLM performs no deviation math.
    /// </param>
    /// <param name="triggeringLog">
    /// The logged workout that triggered the adaptation, passed RAW (unsanitized):
    /// this method is the single sanitization owner for the adaptation prompt. Its
    /// note and free-text metric values are sanitized inside the assembler via
    /// <c>IRecentLogSanitizer</c> (R-068 / DEC-059), and the rendered line is
    /// delimiter-escaped before being placed in the nonce-delimited recent-logs
    /// section. At MVP-0 this composes the triggering log plus plan-week context
    /// only; the recent-logs section carries a single log.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed adaptation prompt — system + user message.</returns>
    Task<AdaptationPromptComposition> ComposeForAdaptationAsync(
        PlanProjectionDto plan,
        EscalationLevel escalationLevel,
        SafetyTier safetyTier,
        DeviationResult deviation,
        LoggedWorkoutDetail triggeringLog,
        CancellationToken ct = default);

    /// <summary>
    /// Composes the system prompt + user message for the interactive-conversation
    /// intent classifier (Slice 4B / DEC-085 D3). Loads the byte-stable classifier
    /// system prompt and renders the runner's CURRENT message — sanitized and
    /// spotlight-wrapped via the DEC-059 sanitizer (this assembler is the single
    /// sanitization owner) — alongside today's date for relative-date resolution.
    /// </summary>
    /// <param name="today">The runner's app-local calendar date, for resolving relative dates in the message.</param>
    /// <param name="userMessage">The runner's RAW chat message; sanitized inside the assembler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed classifier prompt — system + user + sanitization audit trail.</returns>
    Task<ClassificationPromptComposition> ComposeForClassificationAsync(
        DateOnly today,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Composes the system prompt + grounded user message for a streamed conversational
    /// answer (Slice 4B / DEC-085). Reuses <c>coaching-system.v1</c> (the register
    /// re-tuned in Slice 4A — no further prompt re-tune) and grounds the answer in the
    /// current plan, recent logged workouts (newest-first, sanitized + spotlight-wrapped
    /// via <c>IRecentLogSanitizer</c>), recent interactive turns, and the runner's CURRENT
    /// message (sanitized + spotlight-wrapped — single sanitization owner). Per-shape
    /// context routing is out of scope (a single fixed assembly); a separate runner-profile
    /// block is omitted at MVP-0 (the DEC-080 posture) — the plan goal plus recent logs
    /// ground the answer.
    /// </summary>
    /// <param name="plan">The current plan projection, or null when the runner has no active plan.</param>
    /// <param name="recentLogs">Recent logged workouts in any order, passed RAW; the assembler sorts newest-first and sanitizes before rendering.</param>
    /// <param name="recentTurns">Recent non-errored interactive turns, oldest-first, for dialogue continuity.</param>
    /// <param name="userMessage">The runner's RAW chat message; sanitized inside the assembler.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed conversation prompt — system + grounded user message + sanitization audit trail.</returns>
    Task<ConversationPromptComposition> ComposeForConversationAsync(
        PlanProjectionDto? plan,
        IReadOnlyList<LoggedWorkoutDetail> recentLogs,
        IReadOnlyList<ConversationContextTurn> recentTurns,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Composes the system prompt + user message for the short acknowledgment turn the coach
    /// streams after a conversational-logging confirm commits its log and the adaptation has
    /// run (Slice 4B PR5 / DEC-085). Reuses <c>coaching-system.v1</c> (the gruff-direct register
    /// re-tuned in Slice 4A — no further prompt re-tune) so the ack inherits the voice lock. The
    /// user message renders the deterministic logged-run facts (date, runner-stated distance,
    /// duration, completion), the adaptation outcome cue for <paramref name="outcome"/>, and the
    /// runner's note when present — sanitized + spotlight-wrapped via the DEC-059 sanitizer (this
    /// assembler is the single sanitization owner). The instruction forbids inventing the specific
    /// plan change: the deterministic plan diff is authoritative and rendered separately on the
    /// plan timeline, so the ack points at the plan rather than restating the change.
    /// </summary>
    /// <param name="loggedDraft">The confirmed workout draft just committed; supplies the run facts and the optional note.</param>
    /// <param name="outcome">The plan-change kind the deterministic adaptation resolved, driving the outcome cue.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The composed ack prompt — system + user message + sanitization audit trail.</returns>
    Task<AckPromptComposition> ComposeForAckAsync(
        StructuredLogDraft loggedDraft,
        AdaptationKind outcome,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the token count for a given text using the character ratio method
    /// (characters / 4 with a 10% safety margin).
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated token count.</returns>
    int EstimateTokens(string text);
}
