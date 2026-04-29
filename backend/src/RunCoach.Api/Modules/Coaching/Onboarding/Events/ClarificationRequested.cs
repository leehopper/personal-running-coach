using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Records that the assistant flagged the runner's input as ambiguous and asked for clarification.
/// </summary>
/// <param name="Topic">The topic the clarification applies to.</param>
/// <param name="Reason">Human-readable reason the assistant requested clarification.</param>
/// <param name="RequestedAt">Wall-clock time the clarification request was emitted.</param>
public sealed record ClarificationRequested(
    OnboardingTopic Topic,
    string Reason,
    DateTimeOffset RequestedAt);
