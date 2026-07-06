namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Result returned by <see cref="SubmitStructuredAnswersHandler"/> to
/// <c>OnboardingController.SubmitAnswers</c> and memoized by
/// <see cref="Infrastructure.Idempotency.IIdempotencyStore"/> so a duplicate submission replays
/// the same outcome without re-appending events or generating a second plan. The controller reads
/// the authoritative onboarding state from a post-commit projection reload; this result carries
/// only the handler's terminal decision for logging and idempotent replay.
/// </summary>
/// <param name="PlanGenerated">True when the completion gate was satisfied and a plan was generated this submission (or on the memoized original submission).</param>
/// <param name="PlanId">The generated plan id when <paramref name="PlanGenerated"/> is true; otherwise null.</param>
/// <param name="TopicsCaptured">The number of topic answers appended in this submission.</param>
public sealed record SubmitStructuredAnswersResult(
    bool PlanGenerated,
    Guid? PlanId,
    int TopicsCaptured);
