namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Stream-completion event. Emitted exactly once at the end of the terminal-branch handler
/// after plan generation succeeds. Carries the generated plan id for response correlation.
/// </summary>
/// <param name="PlanId">The generated plan's id.</param>
/// <param name="CompletedAt">Wall-clock time onboarding completed.</param>
public sealed record OnboardingCompleted(
    Guid PlanId,
    DateTimeOffset CompletedAt);
