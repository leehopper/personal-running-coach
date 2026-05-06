namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// GET /api/v1/onboarding/state response payload (Slice 1 § Unit 1 R01.10).
/// Surfaces the runner's current onboarding view + the deterministic
/// completion-gate verdict so the chat UI can resume the flow without a
/// separate roundtrip to compute progress.
/// </summary>
/// <param name="UserId">The runner's user id.</param>
/// <param name="Status">Lifecycle status of the onboarding stream.</param>
/// <param name="CurrentTopic">The most recently asked topic, or null on a fresh stream.</param>
/// <param name="CompletedTopics">Number of topics with a captured answer.</param>
/// <param name="TotalTopics">Total topics for the runner (5 when not race-training, 6 when race-training).</param>
/// <param name="IsComplete">
/// True when the onboarding stream is in <see cref="OnboardingStatus.Completed"/> AND the
/// deterministic completion gate is satisfied — surfaces the gate verdict to the
/// chat UI's resume-flow logic.
/// </param>
/// <param name="OutstandingClarifications">
/// Topics with an unresolved <see cref="ClarificationRequested"/> event. The completion
/// gate fails while this list is non-empty.
/// </param>
/// <param name="PrimaryGoal">Captured PrimaryGoal answer or null.</param>
/// <param name="TargetEvent">Captured TargetEvent answer or null.</param>
/// <param name="CurrentFitness">Captured CurrentFitness answer or null.</param>
/// <param name="WeeklySchedule">Captured WeeklySchedule answer or null.</param>
/// <param name="InjuryHistory">Captured InjuryHistory answer or null.</param>
/// <param name="Preferences">Captured Preferences answer or null.</param>
/// <param name="CurrentPlanId">Currently active plan id once <see cref="PlanLinkedToUser"/> has fired; null otherwise.</param>
public sealed record OnboardingStateDto(
    Guid UserId,
    OnboardingStatus Status,
    OnboardingTopic? CurrentTopic,
    int CompletedTopics,
    int TotalTopics,
    bool IsComplete,
    IReadOnlyList<OnboardingTopic> OutstandingClarifications,
    PrimaryGoalAnswer? PrimaryGoal,
    TargetEventAnswer? TargetEvent,
    CurrentFitnessAnswer? CurrentFitness,
    WeeklyScheduleAnswer? WeeklySchedule,
    InjuryHistoryAnswer? InjuryHistory,
    PreferencesAnswer? Preferences,
    Guid? CurrentPlanId);
