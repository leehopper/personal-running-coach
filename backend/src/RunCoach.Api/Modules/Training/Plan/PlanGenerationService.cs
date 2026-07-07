using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Coaching.Onboarding;
using RunCoach.Api.Modules.Coaching.Prompts;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Plain DI service implementing the six-call macro/meso/micro structured-output
/// chain per Slice 1 § Unit 2 R02.4-R02.6 (DEC-057 / R-066). The macro tier carries a
/// bounded corrective-hint retry on deterministic-validator rejection (DEC-087), so a run may
/// make more than six calls. Returns the resulting
/// events as a list — the caller stages them on its own <c>IDocumentSession</c>
/// inside one Marten transaction so the entire plan + onboarding-completion writes
/// commit atomically (R-069 / DEC-060).
/// </summary>
/// <remarks>
/// <para>
/// The service does NOT inject <c>IDocumentSession</c>, does NOT call
/// <c>SaveChangesAsync</c>, and does NOT touch any aggregate or projection. It is
/// pure orchestration over <see cref="IContextAssembler"/> and
/// <see cref="ICoachingLlm"/> calls, returning <c>IReadOnlyList&lt;object&gt;</c> for
/// the caller to pass straight to <c>session.Events.StartStream&lt;Plan&gt;</c>.
/// </para>
/// <para>
/// Per-tier prompt suffix: the base user message (cacheable prefix) comes from
/// <see cref="IContextAssembler.ComposeForPlanGenerationAsync"/>; the service
/// appends a tier-specific suffix (macro / meso week-N / micro) AFTER that base.
/// Anthropic prompt-prefix caching with <see cref="CacheControl.Ephemeral1h"/>
/// hashes everything before the breakpoint, so the suffix's variability across
/// the six calls only affects the post-cache tail tokens.
/// </para>
/// </remarks>
public sealed partial class PlanGenerationService : IPlanGenerationService
{
    /// <summary>
    /// OTel <see cref="ActivitySource"/> + <see cref="Meter"/> name shared with
    /// the rest of the LLM observability surface (sanitization spans,
    /// Anthropic SDK custom spans, etc). Already registered with the OTel
    /// pipeline in <c>Program.cs</c> via <c>AddSource("RunCoach.Llm")</c> +
    /// <c>AddMeter("RunCoach.Llm")</c> so emissions land in any configured
    /// exporter (Phoenix via OTLP per R-051) without further wiring.
    /// </summary>
    internal const string ObservabilitySourceName = "RunCoach.Llm";

    /// <summary>
    /// Name of the parent <see cref="Activity"/> that wraps the entire
    /// six-call chain. Phoenix groups its child spans (one per tier) and the
    /// rolled-up <see cref="PlanGenerationCompletedMetricName"/> metric event
    /// under this span on the trace timeline.
    /// </summary>
    internal const string PlanGenerationActivityName = "runcoach.plan.generation";

    /// <summary>
    /// Activity name used for each per-tier child span (macro, meso, micro).
    /// The tier and per-tier index are stamped as activity tags so Phoenix
    /// can color the seven-span pattern (1 onboarding + 6 plan-gen) per
    /// Slice 1 § Unit 2 R02.8.
    /// </summary>
    internal const string TierActivityName = "runcoach.plan.generation.tier";

    /// <summary>
    /// Histogram instrument name carrying the structured plan-generation
    /// completion event per Slice 1 § Unit 2 R02.8: one measurement per
    /// completed plan-generation run, the recorded value is the wall-clock
    /// duration in milliseconds, and the tag bag carries
    /// <c>{ planId, userId, totalCalls, macroOutputChars, mesoOutputCharsTotal,
    /// microOutputChars, durationMs }</c>. The numeric value duplicates
    /// durationMs so consumers that ignore tag bags still see the duration as
    /// the histogram value.
    /// </summary>
    internal const string PlanGenerationCompletedMetricName = "runcoach.plan.generation.completed";

    /// <summary>
    /// Tier tag value for the macro-plan child span.
    /// </summary>
    internal const string TierMacro = "macro";

    /// <summary>
    /// Tier tag value for each meso-week child span.
    /// </summary>
    internal const string TierMeso = "meso";

    /// <summary>
    /// Tier tag value for the micro-workouts child span.
    /// </summary>
    internal const string TierMicro = "micro";

    /// <summary>
    /// Marker label that begins the macro-tier suffix. The label string is part
    /// of the wire bytes — held in a constant so tests can locate it.
    /// </summary>
    internal const string MacroTierLabel = "[TIER: MACRO PLAN]";

    /// <summary>
    /// Marker label that begins the meso-tier suffix. The week number, phase,
    /// and deload-candidate hint are appended on the lines following this label.
    /// </summary>
    internal const string MesoTierLabel = "[TIER: MESO WEEK]";

    /// <summary>
    /// Marker label that begins the micro-tier suffix. The macro context and
    /// week-1 meso template are recapped under this label so the LLM sees the
    /// same shape across the chain.
    /// </summary>
    internal const string MicroTierLabel = "[TIER: MICRO WORKOUTS]";

    /// <summary>
    /// Marker label that begins the per-retry macro correction suffix appended to the macro user
    /// message when a bounded validation-rejection retry fires (DEC-087). The label is part of the
    /// wire bytes — held in a constant so tests can locate it and confirm the attempt-0 message
    /// carries no suffix.
    /// </summary>
    internal const string MacroCorrectionLabel = "[CORRECTION]";

    /// <summary>
    /// Number of meso weeks Slice 1 generates (weeks 1-4). Constant rather than
    /// a setting so the projection's expected event sequence stays stable.
    /// </summary>
    internal const int MesoWeekCount = 4;

    /// <summary>
    /// Total LLM call count per chain: 1 macro + N meso + 1 micro. Single
    /// source of truth so the chain-wide rollup tag and the per-event
    /// histogram tag bag agree.
    /// </summary>
    internal const int TotalCallCount = 1 + MesoWeekCount + 1;

    /// <summary>
    /// Shared <see cref="ActivitySource"/> for the plan-generation chain.
    /// Singleton because <see cref="ActivitySource"/> instances are
    /// thread-safe and registration with the OTel pipeline is by name.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ObservabilitySourceName);

    /// <summary>
    /// Shared <see cref="Meter"/> for the plan-generation completion event.
    /// Singleton for the same reason as <see cref="ActivitySource"/>.
    /// </summary>
    internal static readonly Meter Meter = new(ObservabilitySourceName);

    private static readonly Histogram<double> PlanGenerationCompleted = Meter.CreateHistogram<double>(
        name: PlanGenerationCompletedMetricName,
        unit: "ms",
        description: "Wall-clock duration of one runcoach plan-generation chain (1 macro + 4 meso + 1 micro).");

    /// <summary>
    /// JSON serializer used solely to compute a deterministic per-tier output
    /// size proxy for token estimation. Tracks the same snake_case +
    /// string-enum convention as <c>ClaudeCoachingLlm.StructuredOutputSerializerOptions</c>
    /// so the proxy mirrors the bytes Anthropic actually returned. Phoenix
    /// shows the authoritative token counts on the underlying SDK span; the
    /// metric event here gives a dashboard-friendly rollup view at the
    /// service-orchestration layer.
    /// </summary>
    private static readonly JsonSerializerOptions OutputSizeSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly IContextAssembler _assembler;
    private readonly ICoachingLlm _llm;
    private readonly IPromptStore _promptStore;
    private readonly CoachingLlmSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILocalDateProvider _localDate;
    private readonly ILogger<PlanGenerationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanGenerationService"/> class.
    /// </summary>
    /// <param name="assembler">Context assembler used to compose the cacheable prompt prefix.</param>
    /// <param name="llm">Coaching LLM adapter used to invoke the structured-output chain.</param>
    /// <param name="promptStore">Prompt store consulted to record the active prompt version on <see cref="PlanGenerated"/>.</param>
    /// <param name="settings">Coaching LLM settings — supplies the model id stamped on <see cref="PlanGenerated"/>.</param>
    /// <param name="timeProvider">Time provider for the <see cref="PlanGenerated.GeneratedAt"/> stamp.</param>
    /// <param name="localDate">App-local date provider supplying "today" for the date-aware horizon and the <see cref="PlanGenerated.PlanStartDate"/> anchor (F3 / DEC-082).</param>
    /// <param name="logger">Logger.</param>
    public PlanGenerationService(
        IContextAssembler assembler,
        ICoachingLlm llm,
        IPromptStore promptStore,
        IOptions<CoachingLlmSettings> settings,
        TimeProvider timeProvider,
        ILocalDateProvider localDate,
        ILogger<PlanGenerationService> logger)
    {
        ArgumentNullException.ThrowIfNull(assembler);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(promptStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(localDate);
        ArgumentNullException.ThrowIfNull(logger);

        _assembler = assembler;
        _llm = llm;
        _promptStore = promptStore;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _localDate = localDate;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PlanEventSequence> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileSnapshot);
        ct.ThrowIfCancellationRequested();

        // Wrap the entire chain in a parent span so Phoenix groups the six
        // plan-gen child spans under one trace timeline. The caller's own
        // activity (SubmitStructuredAnswersHandler on onboarding completion or
        // RegeneratePlanHandler, already started by AspNetCore instrumentation)
        // is the ambient parent — `StartActivity` follows the W3C TraceContext
        // ambient flow so the chain re-roots correctly.
        using var chainActivity = ActivitySource.StartActivity(
            PlanGenerationActivityName,
            ActivityKind.Internal);
        chainActivity?.SetTag(PlanGenerationTagNames.PlanId, planId.ToString());
        chainActivity?.SetTag(PlanGenerationTagNames.UserId, userId.ToString());
        chainActivity?.SetTag(PlanGenerationTagNames.PreviousPlanId, previousPlanId?.ToString());

        // Macro attempt counter is hoisted above the try so the failure catch can stamp it on the
        // completion metric alongside the success path (DEC-087 D6).
        var macroAttempts = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Compute the date-aware horizon once: local "today" → the Sunday anchor → the
            // deterministic horizon from the parsed target-event date. Used for the prompt
            // constraint, the macro validator, and the PlanStartDate anchor (F3 / DEC-082).
            var today = _localDate.Today();
            var planStartDate = PlanCalendar.StartOfTrainingWeek(today);
            var raceDate = ResolveTargetEventDate(profileSnapshot);
            var horizon = PlanHorizonCalculator.Compute(planStartDate, raceDate);

            // Compose the cacheable prefix — same bytes for all six calls.
            var composition = await _assembler
                .ComposeForPlanGenerationAsync(profileSnapshot, intent, today, horizon, ct)
                .ConfigureAwait(false);

            var systemPrompt = composition.SystemPrompt;
            var basePrompt = composition.UserMessage;

            LogChainStart(_logger, planId, userId, previousPlanId);

            // Per-call usage counters accumulate across the six structured-output
            // calls so the rollup metric event can publish a chain-wide
            // <c>cache_hit_rate</c> tag (Slice 1 § Unit 2 R02.8).
            var totalUsage = AnthropicUsage.Zero;

            // Tier 1 — macro plan, with a bounded corrective-hint retry on validation rejection
            // (DEC-087, amending the DEC-073/DEC-080 no-re-prompt posture). The macro tier is the
            // first LLM call and nothing has been staged, so a re-roll costs exactly one call and no
            // meso/micro work is wasted. On a deterministic-validator rejection the retry re-invokes
            // the macro tier with a correction suffix naming the arithmetic the model got wrong (the
            // suffix rides the never-cached user message, so the Ephemeral1h system-block cache is
            // untouched — attempt 0 is byte-identical to the no-retry path). After
            // MacroValidationMaxRetries extra attempts a rejection is terminal: throw before any
            // meso/micro work or event staging, so the caller's Marten transaction aborts with nothing
            // committed. User-facing callers map the terminal throw to an error envelope (onboarding →
            // 422); other callers propagate it through the standard error pipeline.
            var maxMacroRetries = Math.Max(0, _settings.MacroValidationMaxRetries);
            MacroPlanOutput macro;
            int macroOutputChars;
            string? macroCorrection = null;
            while (true)
            {
                macroAttempts++;
                var (candidate, macroUsage, candidateChars) = await InvokeTierAsync<MacroPlanOutput>(
                    tier: TierMacro,
                    planId: planId,
                    systemPrompt: systemPrompt,
                    userMessage: BuildMacroUserMessage(basePrompt, macroCorrection),
                    extraTags: null,
                    ct).ConfigureAwait(false);

                // Every attempt's tokens are really spent, so accumulate each into the chain-wide
                // usage rollup even when the attempt is rejected.
                totalUsage = totalUsage.Add(macroUsage);

                var macroValidation = MacroPlanOutputValidator.Validate(candidate, horizon);
                if (macroValidation.IsValid)
                {
                    macro = candidate;
                    macroOutputChars = candidateChars;
                    break;
                }

                if (macroAttempts > maxMacroRetries)
                {
                    LogMacroRejected(_logger, planId, macroValidation.Violation, macroAttempts);
                    throw new PlanGenerationRejectedException(macroValidation.Violation);
                }

                LogMacroRetry(_logger, planId, macroValidation.Violation, macroAttempts);
                macroCorrection = BuildMacroCorrection(macroValidation.Violation, candidate, horizon);
            }

            // Tier 2 — four meso weeks (1..4). Each call carries a per-week context
            // suffix derived from the macro plan's phase list so the LLM knows
            // which phase boundary the week sits inside without re-reading macro.
            var mesoEvents = new List<MesoCycleCreated>(MesoWeekCount);
            var mesoOutputCharsPerWeek = new int[MesoWeekCount];
            for (var week = 1; week <= MesoWeekCount; week++)
            {
                var weekContext = WeekContext.FromMacro(macro, week);
                var capturedWeek = week;
                var (meso, mesoUsage, mesoChars) = await InvokeTierAsync<MesoWeekOutput>(
                    tier: TierMeso,
                    planId: planId,
                    systemPrompt: systemPrompt,
                    userMessage: BuildMesoUserMessage(basePrompt, macro, weekContext),
                    extraTags: a =>
                    {
                        a?.SetTag(PlanGenerationTagNames.WeekIndex, capturedWeek);
                        a?.SetTag(PlanGenerationTagNames.IsDeloadCandidate, weekContext.IsDeloadCandidate);
                    },
                    ct).ConfigureAwait(false);
                totalUsage = totalUsage.Add(mesoUsage);
                mesoOutputCharsPerWeek[week - 1] = mesoChars;
                mesoEvents.Add(new MesoCycleCreated(week, meso));
            }

            // Tier 3 — micro week-1 detail. The user message recaps the macro plan
            // and the week-1 meso so the model has both contexts. The system block
            // remains the cacheable prefix shared with calls 1-5.
            var weekOneMeso = mesoEvents[0].Meso;
            var (micro, microUsage, microOutputChars) = await InvokeTierAsync<MicroWorkoutListOutput>(
                tier: TierMicro,
                planId: planId,
                systemPrompt: systemPrompt,
                userMessage: BuildMicroUserMessage(basePrompt, macro, weekOneMeso),
                extraTags: null,
                ct).ConfigureAwait(false);
            totalUsage = totalUsage.Add(microUsage);

            var promptVersion = _promptStore.GetActiveVersion(ContextAssembler.CoachingPromptId);
            var generatedAt = _timeProvider.GetUtcNow();

            // Assemble the canonical Slice 1 plan event sequence. PlanStartDate anchors
            // week 1, day 0 (Sunday) to the start of the app-local generation week so a
            // logged run's date maps deterministically to a (week, day) slot (slice-2b
            // Unit 1 / DEC-076). The anchor is the same Sunday the horizon was computed
            // against (F3 / DEC-082); the regenerate flow re-anchors automatically because
            // it shares this site.
            var planGenerated = new PlanGenerated(
                PlanId: planId,
                UserId: userId,
                Macro: macro,
                GeneratedAt: generatedAt,
                PlanStartDate: planStartDate,
                PromptVersion: promptVersion,
                ModelId: _settings.ModelId,
                PreviousPlanId: previousPlanId);

            var sequence = new PlanEventSequence(
                Macro: planGenerated,
                Mesos: mesoEvents,
                Micro: new FirstMicroCycleCreated(micro));

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;
            var mesoOutputCharsTotal = 0;
            for (var i = 0; i < mesoOutputCharsPerWeek.Length; i++)
            {
                mesoOutputCharsTotal += mesoOutputCharsPerWeek[i];
            }

            // Actual LLM call volume for this run — NOT the nominal TotalCallCount constant, because a
            // macro validation retry (DEC-087) adds `macroAttempts - 1` extra macro calls. Stamped as
            // `total_calls` so a cost/volume dashboard reflects the recovered-on-retry population the
            // retry exists to make visible (MacroAttempts carries the retry delta separately).
            var totalLlmCalls = macroAttempts + MesoWeekCount + 1;

            // Compute chain-wide cache-hit rate from the accumulated Anthropic
            // usage counters. The denominator is the total number of input tokens
            // the chain "saw" — fresh + cache-creation + cache-read — so the rate
            // expresses the proportion of input the prompt-prefix cache served
            // without re-billing. When all three counters are zero (e.g. a stub
            // LLM in unit tests that emits no usage), the rate is reported as 0.0
            // so the tag is always present per spec § Unit 2 R02.8.
            var totalInputTokens =
                totalUsage.InputTokens
                + totalUsage.CacheCreationInputTokens
                + totalUsage.CacheReadInputTokens;
            var cacheHitRate = totalInputTokens > 0
                ? totalUsage.CacheReadInputTokens / (double)totalInputTokens
                : 0d;

            // Stamp rollup metrics on the chain span so a single trace view shows
            // the totals without scraping the metric exporter.
            chainActivity?.SetTag(PlanGenerationTagNames.TotalCalls, totalLlmCalls);
            chainActivity?.SetTag(PlanGenerationTagNames.DurationMs, durationMs);
            chainActivity?.SetTag(PlanGenerationTagNames.MacroOutputChars, macroOutputChars);
            chainActivity?.SetTag(PlanGenerationTagNames.MacroAttempts, macroAttempts);
            chainActivity?.SetTag(PlanGenerationTagNames.MesoOutputCharsTotal, mesoOutputCharsTotal);
            chainActivity?.SetTag(PlanGenerationTagNames.MicroOutputChars, microOutputChars);
            chainActivity?.SetTag(PlanGenerationTagNames.InputTokensFresh, totalUsage.InputTokens);
            chainActivity?.SetTag(PlanGenerationTagNames.CacheCreationInputTokens, totalUsage.CacheCreationInputTokens);
            chainActivity?.SetTag(PlanGenerationTagNames.CacheReadInputTokens, totalUsage.CacheReadInputTokens);
            chainActivity?.SetTag(PlanGenerationTagNames.OutputTokens, totalUsage.OutputTokens);
            chainActivity?.SetTag(PlanGenerationTagNames.CacheHitRate, cacheHitRate);

            // Emit the structured `runcoach.plan.generation.completed` event on
            // the existing `RunCoach.Llm` Meter per Slice 1 § Unit 2 R02.8. The
            // recorded value is the wall-clock duration in milliseconds; the tag
            // bag carries the dashboardable rollup. Output-char counts are a
            // deterministic local proxy for output tokens — Phoenix shows the
            // authoritative Anthropic token counts on the underlying SDK span,
            // and the metric event here gives a service-orchestration rollup
            // suitable for Phoenix's evaluation dashboard grouping.
            var tags = new TagList
            {
                { PlanGenerationTagNames.PlanId, planId.ToString() },
                { PlanGenerationTagNames.UserId, userId.ToString() },
                { PlanGenerationTagNames.TotalCalls, totalLlmCalls },
                { PlanGenerationTagNames.MacroOutputChars, macroOutputChars },
                { PlanGenerationTagNames.MacroAttempts, macroAttempts },
                { PlanGenerationTagNames.MesoOutputCharsTotal, mesoOutputCharsTotal },
                { PlanGenerationTagNames.MicroOutputChars, microOutputChars },
                { PlanGenerationTagNames.InputTokensFresh, totalUsage.InputTokens },
                { PlanGenerationTagNames.CacheCreationInputTokens, totalUsage.CacheCreationInputTokens },
                { PlanGenerationTagNames.CacheReadInputTokens, totalUsage.CacheReadInputTokens },
                { PlanGenerationTagNames.OutputTokens, totalUsage.OutputTokens },
                { PlanGenerationTagNames.CacheHitRate, cacheHitRate },
                { PlanGenerationTagNames.Outcome, "success" },
            };
            PlanGenerationCompleted.Record(durationMs, tags);

            LogChainComplete(_logger, planId, TotalCallCount);

            return sequence;
        }
        catch (Exception ex)
        {
            // Tag the parent span as failed and record the exception so
            // downstream OTel exporters classify the trace correctly (without
            // this, default `ActivityStatusCode.Unset` reads as success).
            // Emit the completion histogram with `outcome = "failure"` and
            // the exception type so dashboards can group failed runs without
            // scraping the trace store.
            chainActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            chainActivity?.AddException(ex);

            // Stamp the macro-attempt count on the span for the failure path too (DEC-087 D6): a
            // trace-only view of a macro-exhaustion rejection must show how many attempts were made.
            chainActivity?.SetTag(PlanGenerationTagNames.MacroAttempts, macroAttempts);
            stopwatch.Stop();

            var failureTags = new TagList
            {
                { PlanGenerationTagNames.PlanId, planId.ToString() },
                { PlanGenerationTagNames.UserId, userId.ToString() },
                { PlanGenerationTagNames.MacroAttempts, macroAttempts },
                { PlanGenerationTagNames.Outcome, "failure" },
                { PlanGenerationTagNames.ExceptionType, ex.GetType().FullName },
            };
            PlanGenerationCompleted.Record(stopwatch.Elapsed.TotalMilliseconds, failureTags);
            throw;
        }
    }

    /// <summary>
    /// Computes a deterministic per-tier output-size proxy by re-serializing
    /// the structured output to JSON and returning the byte length. The proxy
    /// mirrors the wire bytes Anthropic actually returned so it scales
    /// linearly with output tokens (~4 chars/token for English+JSON).
    /// </summary>
    private static int MeasureOutputChars<T>(T output)
    {
        if (output is null)
        {
            return 0;
        }

        return JsonSerializer.SerializeToUtf8Bytes(output, OutputSizeSerializerOptions).Length;
    }

    /// <summary>
    /// Appends the macro-tier suffix on the cacheable base prompt. Layout: a
    /// blank line, the tier label, and a brief instruction line telling the
    /// LLM which structured output is expected. When <paramref name="correction"/>
    /// is non-null (a bounded validation-rejection retry, DEC-087) it is appended
    /// after a blank-line separator so the model sees the specific defect to fix.
    /// A null correction yields bytes identical to the pre-DEC-087 macro message,
    /// preserving the attempt-0 input-prompt-stability contract.
    /// </summary>
    private static string BuildMacroUserMessage(string basePrompt, string? correction = null)
    {
        var sb = BeginTierMessage(basePrompt, MacroTierLabel, extraCapacity: correction is null ? 128 : 384);
        sb.AppendLine("Generate the periodized macro plan covering the full training horizon.");
        if (correction is not null)
        {
            sb.AppendLine();
            sb.Append(correction);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Builds the deterministic per-retry correction suffix for a rejected macro (DEC-087). It names
    /// the exact arithmetic the model got wrong so the re-roll becomes a narrow reconciliation task:
    /// for <see cref="MacroPlanOutputValidationViolation.PhaseSumMismatch"/> the observed phase-week
    /// sum vs. the declared <c>total_weeks</c>; for
    /// <see cref="MacroPlanOutputValidationViolation.HorizonMismatch"/> the required target weeks vs.
    /// the emitted total. The numbers are recomputed here from the rejected macro + horizon (the
    /// validator returns only the violation discriminator), so the validator stays pure.
    /// </summary>
    private static string BuildMacroCorrection(
        MacroPlanOutputValidationViolation violation,
        MacroPlanOutput macro,
        PlanHorizon horizon)
    {
        switch (violation)
        {
            case MacroPlanOutputValidationViolation.PhaseSumMismatch:
                var phaseWeekSum = 0;
                foreach (var phase in macro.Phases)
                {
                    phaseWeekSum += phase.Weeks;
                }

                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MacroCorrectionLabel} Your previous plan's phase weeks summed to {phaseWeekSum} but total_weeks was {macro.TotalWeeks}. Emit phases whose Weeks values sum EXACTLY to total_weeks.");

            case MacroPlanOutputValidationViolation.HorizonMismatch:
                var targetWeeks = horizon.TargetTotalWeeks ?? macro.TotalWeeks;
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MacroCorrectionLabel} Your previous plan set total_weeks to {macro.TotalWeeks} but the plan must span EXACTLY {targetWeeks} weeks so race week is the final phase's last week. Set total_weeks to {targetWeeks} and make the phase Weeks sum to it.");

            default:
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{MacroCorrectionLabel} Your previous plan failed internal-consistency validation ({violation}). Ensure the phase Weeks values sum exactly to total_weeks.");
        }
    }

    /// <summary>
    /// Appends the meso-tier suffix on the cacheable base prompt. The macro
    /// plan is serialized into a compact recap so the LLM sees the periodized
    /// targets it just produced; the per-week <see cref="WeekContext"/>
    /// follows so call N uses week N's phase + deload hint.
    /// </summary>
    private static string BuildMesoUserMessage(string basePrompt, MacroPlanOutput macro, WeekContext weekContext)
    {
        var sb = BeginTierMessage(basePrompt, MesoTierLabel, extraCapacity: 256);
        AppendMacroRecap(sb, macro);
        sb.AppendLine(CultureInfo.InvariantCulture, $"WeekIndex: {weekContext.WeekIndex}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"PhaseType: {weekContext.PhaseType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"IsDeloadCandidate: {(weekContext.IsDeloadCandidate ? "true" : "false")}");
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"Generate the week template for week {weekContext.WeekIndex}.");
        return sb.ToString();
    }

    /// <summary>
    /// Appends the micro-tier suffix on the cacheable base prompt. Recaps both
    /// the macro plan and the week-1 meso template so the LLM has the
    /// progression context it needs to lay out detailed workouts.
    /// </summary>
    private static string BuildMicroUserMessage(
        string basePrompt,
        MacroPlanOutput macro,
        MesoWeekOutput weekOneMeso)
    {
        var sb = BeginTierMessage(basePrompt, MicroTierLabel, extraCapacity: 512);
        AppendMacroRecap(sb, macro);
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 phase: {weekOneMeso.PhaseType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 weekly target km: {weekOneMeso.WeeklyTargetKm}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 is deload: {(weekOneMeso.IsDeloadWeek ? "true" : "false")}");
        sb.AppendLine("Generate the detailed workouts for week 1, one per scheduled run day.");
        return sb.ToString();
    }

    /// <summary>
    /// Composes the StringBuilder boilerplate shared by the three tier-suffix
    /// builders: pre-sized buffer, base prompt, blank-line separator, tier
    /// label. Each tier-specific builder appends its own per-call content
    /// after this prefix and returns the final string. Single source of truth
    /// for the prefix shape so the tier builders stay tiny and consistent.
    /// </summary>
    private static StringBuilder BeginTierMessage(string basePrompt, string tierLabel, int extraCapacity)
    {
        var sb = new StringBuilder(basePrompt.Length + extraCapacity);
        sb.Append(basePrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(tierLabel);
        return sb;
    }

    private static void AppendMacroRecap(StringBuilder sb, MacroPlanOutput macro)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"Macro goal: {macro.GoalDescription}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Macro total weeks: {macro.TotalWeeks}");
        sb.AppendLine("Phases:");
        foreach (var phase in macro.Phases)
        {
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"  - {phase.PhaseType}: {phase.Weeks} weeks ({phase.WeeklyDistanceStartKm}-{phase.WeeklyDistanceEndKm} km/wk, {phase.IntensityDistribution})");
        }
    }

    /// <summary>
    /// Parses the onboarding target-event date to a calendar <see cref="DateOnly"/>, or null when
    /// no parseable event date is captured. The onboarding answer stores the date as an ISO
    /// <c>yyyy-MM-dd</c> string (<c>TargetEventAnswer.EventDateIso</c>); a missing event or an
    /// unparseable string yields null (general-fitness behavior).
    /// </summary>
    private static DateOnly? ResolveTargetEventDate(OnboardingView view)
    {
        var iso = view.TargetEvent?.EventDateIso;
        if (string.IsNullOrWhiteSpace(iso))
        {
            return null;
        }

        return DateOnly.TryParseExact(
            iso,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Plan generation chain start: PlanId={PlanId} UserId={UserId} PreviousPlanId={PreviousPlanId}")]
    private static partial void LogChainStart(
        ILogger logger,
        Guid planId,
        Guid userId,
        Guid? previousPlanId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Plan generation chain complete: PlanId={PlanId} EventCount={EventCount}")]
    private static partial void LogChainComplete(
        ILogger logger,
        Guid planId,
        int eventCount);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Macro plan rejected by validation (retries exhausted): PlanId={PlanId} Violation={Violation} Attempts={Attempts}")]
    private static partial void LogMacroRejected(
        ILogger logger,
        Guid planId,
        MacroPlanOutputValidationViolation violation,
        int attempts);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Macro plan rejected, retrying with correction: PlanId={PlanId} Violation={Violation} Attempt={Attempt}")]
    private static partial void LogMacroRetry(
        ILogger logger,
        Guid planId,
        MacroPlanOutputValidationViolation violation,
        int attempt);

    /// <summary>
    /// Wraps one tier-level structured-output call: opens a per-tier child
    /// span, stamps the canonical tier tags, runs the LLM's structured-output
    /// call against the cacheable prefix + tier-specific suffix, measures
    /// the per-tier output-size proxy, and returns the deserialized result
    /// alongside the call's usage counters and char count. Lifts the macro,
    /// meso, and micro tier blocks onto a single shape so OTel emission and
    /// LLM-call wiring don't drift across the three sites.
    /// </summary>
    private async Task<(T Result, AnthropicUsage Usage, int OutputChars)> InvokeTierAsync<T>(
        string tier,
        Guid planId,
        string systemPrompt,
        string userMessage,
        Action<Activity?>? extraTags,
        CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity(TierActivityName, ActivityKind.Internal);
        activity?.SetTag(PlanGenerationTagNames.Tier, tier);
        activity?.SetTag(PlanGenerationTagNames.PlanId, planId.ToString());
        extraTags?.Invoke(activity);

        try
        {
            var (result, usage) = await _llm
                .GenerateStructuredAsync<T>(
                    systemPrompt,
                    userMessage,
                    schema: null,
                    cacheControl: CacheControl.Ephemeral1h,
                    ct)
                .ConfigureAwait(false);

            var outputChars = MeasureOutputChars(result);
            activity?.SetTag(PlanGenerationTagNames.OutputChars, outputChars);
            return (result, usage, outputChars);
        }
        catch (Exception ex)
        {
            // Tag the per-tier span as failed and record the exception so the
            // parent chain span (which sets its own error status in the outer
            // catch) keeps the failing-tier identity (`tier` tag already set
            // above) visible in the trace.
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
