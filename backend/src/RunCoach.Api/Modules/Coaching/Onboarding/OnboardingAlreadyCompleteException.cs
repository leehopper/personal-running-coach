namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Thrown by <see cref="OnboardingTurnHandler"/> when a runner attempts to submit
/// a fresh turn against a stream that already terminated with
/// <see cref="OnboardingCompleted"/>. The controller surface translates the
/// exception into HTTP 409 Conflict with an RFC 7807 ProblemDetails body per
/// the onboarding state-engine feature spec.
/// </summary>
public sealed class OnboardingAlreadyCompleteException(Guid userId)
    : InvalidOperationException($"Onboarding is already complete for user {userId}; submit a regenerate request instead of a fresh turn.")
{
    /// <summary>Gets the user id whose stream is already terminal.</summary>
    public Guid UserId { get; } = userId;
}
