namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Progress indicator surface for the chat UI's <c>TopicProgressIndicator</c>.
/// Property names mirror the flat counters on <see cref="OnboardingStateDto"/>
/// so the discriminated-union Zod schema on the frontend can share the same
/// shape — every progress payload on the wire reads
/// <c>{ completedTopics, totalTopics }</c>.
/// </summary>
/// <param name="CompletedTopics">Count of topics that have a captured answer.</param>
/// <param name="TotalTopics">Total number of topics in the onboarding flow (six per DEC-047).</param>
public sealed record OnboardingProgressDto(
    int CompletedTopics,
    int TotalTopics);
