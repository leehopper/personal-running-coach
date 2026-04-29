namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Lifecycle status of the per-user onboarding stream. Tracks whether the runner has
/// started, is mid-flow, or has completed the six-topic intake. Values are explicitly
/// numbered so reordering members in source does not change Marten event payloads or
/// JSON wire encoding.
/// </summary>
public enum OnboardingStatus
{
    /// <summary>
    /// Stream not yet created (no <see cref="OnboardingStarted"/> applied).
    /// Default value of a freshly-instantiated <see cref="OnboardingView"/>.
    /// </summary>
    NotStarted = 0,

    /// <summary>
    /// Stream is open; the runner is actively answering topics.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Terminal state: <see cref="OnboardingCompleted"/> has been applied. No further
    /// events expected on the stream beyond audit. Plan generation has succeeded.
    /// </summary>
    Completed = 2,
}
