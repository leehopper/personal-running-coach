using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Typed extraction result for the <see cref="OnboardingTopic.Preferences"/> topic.
/// </summary>
public sealed record PreferencesExtraction(double Confidence, PreferencesAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.Preferences;
}
