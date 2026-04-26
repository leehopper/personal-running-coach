using System.Globalization;
using System.Text;
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
/// chain per Slice 1 § Unit 2 R02.4-R02.6 (DEC-057 / R-066). Returns the resulting
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
    /// Number of meso weeks Slice 1 generates (weeks 1-4). Constant rather than
    /// a setting so the projection's expected event sequence stays stable.
    /// </summary>
    internal const int MesoWeekCount = 4;

    private readonly IContextAssembler _assembler;
    private readonly ICoachingLlm _llm;
    private readonly IPromptStore _promptStore;
    private readonly CoachingLlmSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlanGenerationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanGenerationService"/> class.
    /// </summary>
    /// <param name="assembler">Context assembler used to compose the cacheable prompt prefix.</param>
    /// <param name="llm">Coaching LLM adapter used to invoke the structured-output chain.</param>
    /// <param name="promptStore">Prompt store consulted to record the active prompt version on <see cref="PlanGenerated"/>.</param>
    /// <param name="settings">Coaching LLM settings — supplies the model id stamped on <see cref="PlanGenerated"/>.</param>
    /// <param name="timeProvider">Time provider for the <see cref="PlanGenerated.GeneratedAt"/> stamp.</param>
    /// <param name="logger">Logger.</param>
    public PlanGenerationService(
        IContextAssembler assembler,
        ICoachingLlm llm,
        IPromptStore promptStore,
        IOptions<CoachingLlmSettings> settings,
        TimeProvider timeProvider,
        ILogger<PlanGenerationService> logger)
    {
        ArgumentNullException.ThrowIfNull(assembler);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(promptStore);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _assembler = assembler;
        _llm = llm;
        _promptStore = promptStore;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<object>> GeneratePlanAsync(
        OnboardingView profileSnapshot,
        Guid userId,
        Guid planId,
        RegenerationIntent? intent,
        Guid? previousPlanId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(profileSnapshot);
        ct.ThrowIfCancellationRequested();

        // Compose the cacheable prefix — same bytes for all six calls.
        var composition = await _assembler
            .ComposeForPlanGenerationAsync(profileSnapshot, intent, ct)
            .ConfigureAwait(false);

        var systemPrompt = composition.SystemPrompt;
        var basePrompt = composition.UserMessage;

        LogChainStart(_logger, planId, userId, previousPlanId);

        // Tier 1 — macro plan.
        var macro = await _llm
            .GenerateStructuredAsync<MacroPlanOutput>(
                systemPrompt,
                BuildMacroUserMessage(basePrompt),
                schema: null,
                cacheControl: CacheControl.Ephemeral1h,
                ct)
            .ConfigureAwait(false);

        // Tier 2 — four meso weeks (1..4). Each call carries a per-week context
        // suffix derived from the macro plan's phase list so the LLM knows
        // which phase boundary the week sits inside without re-reading macro.
        var mesoEvents = new List<MesoCycleCreated>(MesoWeekCount);
        for (var week = 1; week <= MesoWeekCount; week++)
        {
            var weekContext = WeekContext.FromMacro(macro, week);
            var meso = await _llm
                .GenerateStructuredAsync<MesoWeekOutput>(
                    systemPrompt,
                    BuildMesoUserMessage(basePrompt, macro, weekContext),
                    schema: null,
                    cacheControl: CacheControl.Ephemeral1h,
                    ct)
                .ConfigureAwait(false);

            mesoEvents.Add(new MesoCycleCreated(week, meso));
        }

        // Tier 3 — micro week-1 detail. The user message recaps the macro plan
        // and the week-1 meso so the model has both contexts. The system block
        // remains the cacheable prefix shared with calls 1-5.
        var weekOneMeso = mesoEvents[0].Meso;
        var micro = await _llm
            .GenerateStructuredAsync<MicroWorkoutListOutput>(
                systemPrompt,
                BuildMicroUserMessage(basePrompt, macro, weekOneMeso),
                schema: null,
                cacheControl: CacheControl.Ephemeral1h,
                ct)
            .ConfigureAwait(false);

        var promptVersion = _promptStore.GetActiveVersion(ContextAssembler.CoachingPromptId);

        // Assemble the canonical Slice 1 plan event sequence.
        var planGenerated = new PlanGenerated(
            PlanId: planId,
            UserId: userId,
            Macro: macro,
            GeneratedAt: _timeProvider.GetUtcNow(),
            PromptVersion: promptVersion,
            ModelId: _settings.ModelId,
            PreviousPlanId: previousPlanId);

        var events = new List<object>(1 + MesoWeekCount + 1)
        {
            planGenerated,
        };
        events.AddRange(mesoEvents);
        events.Add(new FirstMicroCycleCreated(micro));

        LogChainComplete(_logger, planId, events.Count);

        return events;
    }

    /// <summary>
    /// Appends the macro-tier suffix on the cacheable base prompt. Layout: a
    /// blank line, the tier label, and a brief instruction line telling the
    /// LLM which structured output is expected.
    /// </summary>
    private static string BuildMacroUserMessage(string basePrompt)
    {
        var sb = new StringBuilder(basePrompt.Length + 128);
        sb.Append(basePrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(MacroTierLabel);
        sb.AppendLine("Generate the periodized macro plan covering the full training horizon.");
        return sb.ToString();
    }

    /// <summary>
    /// Appends the meso-tier suffix on the cacheable base prompt. The macro
    /// plan is serialized into a compact recap so the LLM sees the periodized
    /// targets it just produced; the per-week <see cref="WeekContext"/>
    /// follows so call N uses week N's phase + deload hint.
    /// </summary>
    private static string BuildMesoUserMessage(string basePrompt, MacroPlanOutput macro, WeekContext weekContext)
    {
        var sb = new StringBuilder(basePrompt.Length + 256);
        sb.Append(basePrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(MesoTierLabel);
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
        var sb = new StringBuilder(basePrompt.Length + 512);
        sb.Append(basePrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(MicroTierLabel);
        AppendMacroRecap(sb, macro);
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 phase: {weekOneMeso.PhaseType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 weekly target km: {weekOneMeso.WeeklyTargetKm}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Week 1 is deload: {(weekOneMeso.IsDeloadWeek ? "true" : "false")}");
        sb.AppendLine("Generate the detailed workouts for week 1, one per scheduled run day.");
        return sb.ToString();
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

    /// <summary>
    /// Per-week context derived from the macro plan's phase list. Tells the
    /// meso-tier LLM call which phase the week sits in and whether it is the
    /// last week of a phase chunk that includes a deload week (the candidate
    /// week for volume reduction).
    /// </summary>
    /// <param name="WeekIndex">1-based week number within the plan (1..4 in Slice 1).</param>
    /// <param name="PhaseType">The periodization phase this week sits inside.</param>
    /// <param name="IsDeloadCandidate">
    /// Whether this week is the last week of a phase chunk that includes a
    /// deload week — a hint to the LLM that this week may be the deload. The
    /// LLM ultimately decides whether to flag the week with
    /// <c>MesoWeekOutput.IsDeloadWeek = true</c>.
    /// </param>
    internal sealed record WeekContext(int WeekIndex, PhaseType PhaseType, bool IsDeloadCandidate)
    {
        /// <summary>
        /// Derives the <see cref="WeekContext"/> for the given 1-based
        /// <paramref name="weekIndex"/> from the macro plan. Walks the phase
        /// list summing each phase's <c>Weeks</c> and returns the phase that
        /// owns the requested week. If the requested week falls past the last
        /// declared phase (defensive: macro might under-declare in pathological
        /// LLM outputs), the last phase is returned.
        /// </summary>
        public static WeekContext FromMacro(MacroPlanOutput macro, int weekIndex)
        {
            ArgumentNullException.ThrowIfNull(macro);
            if (weekIndex < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(weekIndex), weekIndex, "Week index is 1-based.");
            }

            if (macro.Phases.Length == 0)
            {
                // No phases declared — emit a defensive Base/no-deload context
                // so the meso call still proceeds. The macro itself will fail
                // downstream validation if this happens; logging is the
                // caller's responsibility via the analyzer pipeline.
                return new WeekContext(weekIndex, PhaseType.Base, IsDeloadCandidate: false);
            }

            var cumulative = 0;
            foreach (var phase in macro.Phases)
            {
                var phaseEnd = cumulative + phase.Weeks;
                if (weekIndex <= phaseEnd)
                {
                    var isLastWeekOfPhase = weekIndex == phaseEnd;
                    return new WeekContext(
                        weekIndex,
                        phase.PhaseType,
                        IsDeloadCandidate: phase.IncludesDeload && isLastWeekOfPhase);
                }

                cumulative = phaseEnd;
            }

            var lastPhase = macro.Phases[^1];
            return new WeekContext(weekIndex, lastPhase.PhaseType, IsDeloadCandidate: false);
        }
    }
}
