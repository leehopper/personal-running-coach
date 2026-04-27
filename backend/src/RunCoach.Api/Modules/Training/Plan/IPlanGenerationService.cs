using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Plain DI service that orchestrates the tiered macro/meso/micro structured-output
/// chain to generate a training plan from a captured onboarding profile snapshot.
/// Per Slice 1 § Unit 2 R02.4-R02.6 / DEC-057 / R-066 this is intentionally NOT a
/// Wolverine handler and NOT a Wolverine command — it is invoked inline by the
/// caller's <c>[AggregateHandler]</c> body so the returned events commit on the
/// caller's <c>IDocumentSession</c> inside one Marten transaction (no
/// <c>IMessageBus.InvokeAsync</c> opening a second session).
/// </summary>
/// <remarks>
/// <para>
/// The service is pure orchestration: it composes the LLM prompt via
/// <c>IContextAssembler.ComposeForPlanGenerationAsync</c>, calls
/// <c>ICoachingLlm.GenerateStructuredAsync</c> exactly six times (1 macro + 4 meso +
/// 1 micro), and returns the resulting events as a list. It does NOT touch
/// <c>IDocumentSession</c>, does NOT call <c>SaveChangesAsync</c>, and does NOT
/// stage events on any stream — the caller (Slice 1 § Unit 1's
/// <c>OnboardingTurnHandler</c> on the terminal turn, or Slice 1 § Unit 5's
/// <c>RegeneratePlanHandler</c>) is responsible for invoking
/// <c>session.Events.StartStream&lt;Plan&gt;(planId, events)</c> on its own session.
/// </para>
/// <para>
/// Failure semantics: the Anthropic SDK's <c>MaxRetries</c> (3 by default) covers
/// transient errors (429, 5xx, network). An unrecoverable failure on any tier
/// (macro, any of the four meso calls, or micro) propagates as an exception — the
/// caller's transactional middleware then rolls back the entire Marten transaction
/// so no partial Plan stream is persisted, no <c>OnboardingCompleted</c> /
/// <c>PlanLinkedToUser</c> events are appended, and the EF projection's
/// <c>UserProfile.CurrentPlanId</c> stays at its prior value.
/// </para>
/// </remarks>
public interface IPlanGenerationService
{
    /// <summary>
    /// Generates the per-user plan event sequence from a captured onboarding
    /// profile snapshot. Runs the six-call structured-output chain (macro → 4
    /// meso → micro) with Anthropic prompt-cache breakpoints set to the 1-hour
    /// ephemeral tier so calls 2-6 hit the prefix cache.
    /// </summary>
    /// <param name="profileSnapshot">
    /// The completed <see cref="OnboardingView"/> projection — the captured
    /// answers across the six DEC-047 topics serve as the profile for plan
    /// generation. The snapshot is read directly; the chain does NOT replay
    /// the onboarding event stream on each macro/meso/micro call.
    /// </param>
    /// <param name="userId">The runner's user id; threaded onto the returned <see cref="PlanGenerated"/> event.</param>
    /// <param name="planId">
    /// The new Plan stream's id (the caller passes
    /// <c>CombGuidIdGeneration.NewGuid()</c>); also doubles as the per-user-plan
    /// stream id when the caller calls <c>session.Events.StartStream&lt;Plan&gt;(planId, events)</c>.
    /// </param>
    /// <param name="intent">
    /// Optional regeneration intent free-text supplied by the runner via
    /// Settings → Plan (Slice 1 § Unit 5). When non-null it is appended at the
    /// END of the plan-generation user message under the stable label
    /// <c>[Regeneration intent provided by user]</c> so the prefix above it
    /// stays byte-identical to the initial-generation prompt. The free-text
    /// MUST already be sanitized by the caller via
    /// <c>IPromptSanitizer.SanitizeAsync(intent.FreeText, PromptSection.RegenerationIntentFreeText, ct)</c>.
    /// </param>
    /// <param name="previousPlanId">
    /// The prior plan id when this generation came from the regenerate-from-settings
    /// flow, or <see langword="null"/> for the initial onboarding-driven generation.
    /// Threaded onto the returned <see cref="PlanGenerated"/> event so the
    /// projection retains audit linkage to the prior plan without a schema bump.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An ordered list of events:
    /// <c>[PlanGenerated, MesoCycleCreated×4, FirstMicroCycleCreated]</c> in
    /// exactly that sequence. The caller stages this list onto its own
    /// <c>IDocumentSession</c> via
    /// <c>session.Events.StartStream&lt;Plan&gt;(planId, events)</c>.
    /// </returns>
    Task<IReadOnlyList<object>> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct);
}
