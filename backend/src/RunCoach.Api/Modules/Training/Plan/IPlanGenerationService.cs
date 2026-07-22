using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Plain DI service that orchestrates the tiered macro/meso/micro structured-output
/// chain to generate a training plan from a captured onboarding profile snapshot.
/// Per spec § Unit 2 R02.4-R02.6 / DEC-057 / R-066 this is intentionally NOT a
/// Wolverine handler and NOT a Wolverine command — it is invoked inline by the
/// caller's static handler body so the returned events commit on the caller's
/// <c>IDocumentSession</c> inside one Marten transaction (no
/// <c>IMessageBus.InvokeAsync</c> opening a second session).
/// </summary>
/// <remarks>
/// <para>
/// The service is pure orchestration: it composes the LLM prompt via
/// <c>IContextAssembler.ComposeForPlanGenerationAsync</c>, calls
/// <c>ICoachingLlm.GenerateStructuredAsync</c> for each tier (1 macro + 4 meso + 1 micro —
/// six on the happy path; the macro tier is re-invoked up to
/// <c>CoachingLlmSettings.MacroValidationMaxRetries</c> extra times when the deterministic macro
/// validator rejects the output, DEC-087), and returns the resulting events as a
/// <see cref="PlanEventSequence"/>.
/// It does NOT touch <c>IDocumentSession</c>, does NOT call <c>SaveChangesAsync</c>,
/// and does NOT stage events on any stream — the caller
/// (<c>SubmitStructuredAnswersHandler</c> on onboarding completion, or
/// <c>RegeneratePlanHandler</c>) is responsible for invoking
/// <c>session.Events.StartStream&lt;PlanProjectionDto&gt;(planId, planEvents.ToEvents())</c>
/// on its own session.
/// </para>
/// <para>
/// Failure semantics: the Anthropic SDK's <c>MaxRetries</c> (2 by default, i.e. up to
/// 3 attempts) covers transient errors (429, 5xx, network). A macro that fails deterministic
/// validation is re-generated with a corrective hint up to the bounded
/// <c>MacroValidationMaxRetries</c> budget (DEC-087); on exhaustion a
/// <c>PlanGenerationRejectedException</c> propagates. An unrecoverable failure on
/// any tier (macro, any of the four meso calls, or micro) propagates as a
/// <c>TransientCoachingLlmException</c> / <c>PermanentCoachingLlmException</c> (DEC-073). Any of
/// these — the caller's transactional middleware then rolls back the entire Marten transaction
/// so no partial Plan stream is persisted, no <c>OnboardingCompleted</c> /
/// <c>PlanLinkedToUser</c> events are appended, and the EF projection's
/// <c>RunnerOnboardingProfile.CurrentPlanId</c> stays at its prior value.
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
    /// stream id when the caller calls
    /// <c>session.Events.StartStream&lt;PlanProjectionDto&gt;(planId, planEvents.ToEvents())</c>.
    /// </param>
    /// <param name="intent">
    /// Optional regeneration intent free-text supplied by the runner via
    /// Settings → Plan. When non-null it is appended at the
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
    /// A strongly-typed <see cref="PlanEventSequence"/> wrapping the
    /// <c>[PlanGenerated, MesoCycleCreated×4, FirstMicroCycleCreated]</c>
    /// sequence. Call <see cref="PlanEventSequence.ToEvents"/> to flatten to
    /// the <c>IReadOnlyList&lt;object&gt;</c> shape Marten's
    /// <c>session.Events.StartStream</c> expects.
    /// </returns>
    Task<PlanEventSequence> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct);

    /// <summary>
    /// Generates the meso template and/or the detailed micro workouts for ONE target week of an
    /// already-generated plan — the rolling-horizon extension seam (DEC-090). This PR ships the seam
    /// only: no handler and no sweeper call it yet (PR2/PR3). It reuses the tiered structured-output
    /// machinery + the bounded corrective-hint retry shape that <see cref="GeneratePlanAsync"/>
    /// established for the macro and micro tiers.
    /// </summary>
    /// <param name="profileSnapshot">
    /// The runner's completed <see cref="OnboardingView"/>, used only to compose the cacheable
    /// prompt prefix via the unchanged <c>IContextAssembler.ComposeForPlanGenerationAsync</c>.
    /// </param>
    /// <param name="userId">The runner's user id; threaded onto the parent OTel activity.</param>
    /// <param name="planId">The plan stream id being extended; threaded onto the parent OTel activity.</param>
    /// <param name="macro">
    /// The plan's already-generated <see cref="MacroPlanOutput"/> — the caller reads it off the
    /// <c>PlanProjectionDto</c>. Drives the per-week <c>WeekContext</c> and the macro recap in the
    /// tier-suffix prompts.
    /// </param>
    /// <param name="planStartDate">
    /// The plan's fixed anchor date, used together with <paramref name="targetEventDate"/> to
    /// recompute the deterministic horizon. NOT re-derived from today, so the prompt prefix reflects
    /// the plan's real anchor rather than a fresh computation.
    /// </param>
    /// <param name="targetEventDate">
    /// The plan's parsed target event date (from the projection), or <see langword="null"/> for a
    /// general-fitness plan. Paired with <paramref name="planStartDate"/> to recompute the horizon.
    /// </param>
    /// <param name="targetWeekIndex">The 1-based week to generate.</param>
    /// <param name="existingMesoWeek">
    /// When non-null, the target week's meso is already populated (the micro-only backfill case —
    /// e.g. every plan live today at week 2): meso generation is skipped and this becomes the source
    /// of truth for the micro tier. When <see langword="null"/>, the meso tier is generated first.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="WeekGenerationResult"/> wrapping the events generated for the target week.
    /// </returns>
    /// <exception cref="MesoWeekRejectedException">
    /// Thrown when the meso tier still fails <c>MesoWeekOutputValidator</c> after the bounded
    /// <c>CoachingLlmSettings.MesoValidationMaxRetries</c> budget. Nothing is partially returned —
    /// the whole call fails so the eventual caller's transaction aborts.
    /// </exception>
    /// <exception cref="MesoMicroConsistencyRejectedException">
    /// Thrown when the micro tier still fails <c>MesoMicroConsistencyValidator</c> against the
    /// target week's meso after the bounded <c>CoachingLlmSettings.MicroValidationMaxRetries</c>
    /// budget. Nothing is partially returned — the whole call fails so the eventual caller's
    /// transaction aborts.
    /// </exception>
    Task<WeekGenerationResult> GenerateWeekAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        MacroPlanOutput macro,
        DateOnly planStartDate,
        DateOnly? targetEventDate,
        int targetWeekIndex,
        MesoWeekOutput? existingMesoWeek,
        CancellationToken ct);
}
