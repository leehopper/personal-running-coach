namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Identifies a user-controlled free-text section being fed into the assembled
/// LLM prompt. Each value selects a per-section sanitization policy
/// (Unicode-strip + regex tier + containment delimiter) per Slice 1 § Unit 6.
/// </summary>
/// <remarks>
/// This enum is the policy key for <see cref="IPromptSanitizer"/>. It is
/// distinct from <c>RunCoach.Api.Modules.Coaching.Models.PromptSection</c>,
/// which is a content+token-estimate record produced by ContextAssembler.
/// The two types coexist in different namespaces.
/// </remarks>
public enum PromptSection
{
    /// <summary>The user's display name. Light catalog (PI-07 only); short proper noun.</summary>
    UserProfileName,

    /// <summary>Free-text injury description. Full catalog, log-only.</summary>
    UserProfileInjuryNote,

    /// <summary>Free-text race conditions notes. Full catalog, log-only.</summary>
    UserProfileRaceCondition,

    /// <summary>Free-text user constraints (preferences). Full catalog, log-only.</summary>
    UserProfileConstraints,

    /// <summary>Race name (proper noun). Light catalog (PI-07 only).</summary>
    GoalStateRaceName,

    /// <summary>
    /// Free-text workout note. Slice 2 surface; defined here for completeness.
    /// Full catalog, log-only.
    /// </summary>
    TrainingHistoryWorkoutNote,

    /// <summary>
    /// Replayed user message from prior conversation turns. Sanitized at
    /// write time; full catalog, log-only.
    /// </summary>
    ConversationHistoryUserMessage,

    /// <summary>
    /// The current turn's user message. Highest-priority surface — full
    /// catalog with PI-04/PI-05/PI-06 (DAN family) promoted to neutralize-mode.
    /// </summary>
    CurrentUserMessage,

    /// <summary>
    /// Free-text regenerate-plan intent ("reduce volume", "I'm injured").
    /// Full catalog, log-only.
    /// </summary>
    RegenerationIntentFreeText,
}
