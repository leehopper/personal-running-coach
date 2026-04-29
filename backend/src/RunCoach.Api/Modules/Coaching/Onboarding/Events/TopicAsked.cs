using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Records that the deterministic next-topic selector advanced to a new topic for this turn.
/// </summary>
/// <param name="Topic">The topic the assistant is asking about on this turn.</param>
/// <param name="AskedAt">Wall-clock time the topic transition occurred.</param>
public sealed record TopicAsked(
    OnboardingTopic Topic,
    DateTimeOffset AskedAt);
