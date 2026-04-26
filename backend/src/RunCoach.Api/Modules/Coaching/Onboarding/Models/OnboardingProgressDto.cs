namespace RunCoach.Api.Modules.Coaching.Onboarding.Models;

/// <summary>
/// Progress indicator surface for the chat UI's <c>TopicProgressIndicator</c>.
/// </summary>
/// <param name="Completed">Count of topics that have a captured answer.</param>
/// <param name="Total">Total number of topics in the onboarding flow (six per DEC-047).</param>
public sealed record OnboardingProgressDto(
    int Completed,
    int Total);
