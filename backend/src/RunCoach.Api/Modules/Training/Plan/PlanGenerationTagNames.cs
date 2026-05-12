namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// OTel tag-name constants for the plan-generation chain. Centralized so a
/// typo at one call site can't create an orphan tag that downstream
/// dashboards silently miss, and so the SonarAnalyzer S1192 (literal repeated
/// &gt;= 3 times) noise stays out of the chain code. Lifted out of
/// <see cref="PlanGenerationService"/> per the one-type-per-file rule — these
/// constants aren't a serialization model for the enclosing service, so the
/// nested-type carve-out doesn't apply.
/// </summary>
internal static class PlanGenerationTagNames
{
    public const string PlanId = "runcoach.plan.id";
    public const string UserId = "runcoach.user.id";
    public const string PreviousPlanId = "runcoach.plan.previous_id";
    public const string Tier = "runcoach.plan.tier";
    public const string WeekIndex = "runcoach.plan.week_index";
    public const string IsDeloadCandidate = "runcoach.plan.is_deload_candidate";
    public const string OutputChars = "runcoach.plan.output_chars";
    public const string TotalCalls = "runcoach.plan.total_calls";
    public const string DurationMs = "runcoach.plan.duration_ms";
    public const string MacroOutputChars = "runcoach.plan.macro_output_chars";
    public const string MesoOutputCharsTotal = "runcoach.plan.meso_output_chars_total";
    public const string MicroOutputChars = "runcoach.plan.micro_output_chars";
    public const string InputTokensFresh = "runcoach.plan.input_tokens_fresh";
    public const string CacheCreationInputTokens = "runcoach.plan.cache_creation_input_tokens";
    public const string CacheReadInputTokens = "runcoach.plan.cache_read_input_tokens";
    public const string OutputTokens = "runcoach.plan.output_tokens";
    public const string CacheHitRate = "runcoach.plan.cache_hit_rate";
    public const string Outcome = "runcoach.plan.outcome";
    public const string ExceptionType = "runcoach.plan.exception_type";
}
