using System.ComponentModel;
using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Topic-discriminated extraction payload nested inside <see cref="OnboardingTurnOutput"/>.
/// Pattern B: exactly one of the six <c>Normalized*</c> slots is non-null and matches
/// <see cref="Topic"/>. Anthropic constrained decoding rejects <c>oneOf</c>, so the
/// per-slot nullability and Topic/slot alignment cannot be encoded in the JSON Schema
/// the model sees; the <c>required</c> attribute on each slot only enforces presence,
/// not "exactly one non-null". The slice-1a3 surface is the event-record schema and the
/// projections that consume <c>AnswerCaptured</c> events — the runtime guard that
/// rejects malformed turn outputs at the LLM-output boundary lives on the onboarding
/// turn handler and lands with that handler in a downstream slice.
/// </summary>
public sealed record ExtractedAnswer
{
    /// <summary>
    /// Gets the topic this extraction applies to. Must match the single non-null Normalized* slot.
    /// </summary>
    [Description("Topic this extraction applies to. Must match the single non-null Normalized* slot.")]
    public required OnboardingTopic Topic { get; init; }

    /// <summary>
    /// Gets the assistant's confidence in the extraction. Documented range 0.0-1.0; bounds
    /// are not enforced via JSON Schema because Anthropic rejects minimum/maximum keywords.
    /// The handler discards extractions with Confidence below 0.6.
    /// </summary>
    [Description("Assistant's confidence in the extraction. Valid range 0.0 through 1.0. The handler discards extractions below 0.6.")]
    public required double Confidence { get; init; }

    /// <summary>
    /// Gets the normalized PrimaryGoal answer when <see cref="Topic"/> is PrimaryGoal; null otherwise.
    /// </summary>
    [Description("Normalized PrimaryGoal answer. Non-null only when Topic is PrimaryGoal.")]
    public required PrimaryGoalAnswer? NormalizedPrimaryGoal { get; init; }

    /// <summary>
    /// Gets the normalized TargetEvent answer when <see cref="Topic"/> is TargetEvent; null otherwise.
    /// </summary>
    [Description("Normalized TargetEvent answer. Non-null only when Topic is TargetEvent.")]
    public required TargetEventAnswer? NormalizedTargetEvent { get; init; }

    /// <summary>
    /// Gets the normalized CurrentFitness answer when <see cref="Topic"/> is CurrentFitness; null otherwise.
    /// </summary>
    [Description("Normalized CurrentFitness answer. Non-null only when Topic is CurrentFitness.")]
    public required CurrentFitnessAnswer? NormalizedCurrentFitness { get; init; }

    /// <summary>
    /// Gets the normalized WeeklySchedule answer when <see cref="Topic"/> is WeeklySchedule; null otherwise.
    /// </summary>
    [Description("Normalized WeeklySchedule answer. Non-null only when Topic is WeeklySchedule.")]
    public required WeeklyScheduleAnswer? NormalizedWeeklySchedule { get; init; }

    /// <summary>
    /// Gets the normalized InjuryHistory answer when <see cref="Topic"/> is InjuryHistory; null otherwise.
    /// </summary>
    [Description("Normalized InjuryHistory answer. Non-null only when Topic is InjuryHistory.")]
    public required InjuryHistoryAnswer? NormalizedInjuryHistory { get; init; }

    /// <summary>
    /// Gets the normalized Preferences answer when <see cref="Topic"/> is Preferences; null otherwise.
    /// </summary>
    [Description("Normalized Preferences answer. Non-null only when Topic is Preferences.")]
    public required PreferencesAnswer? NormalizedPreferences { get; init; }
}
