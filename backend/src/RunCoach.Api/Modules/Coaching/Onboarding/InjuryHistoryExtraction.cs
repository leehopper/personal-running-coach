using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Typed extraction result for the <see cref="OnboardingTopic.InjuryHistory"/> topic.
/// </summary>
public sealed record InjuryHistoryExtraction(double Confidence, InjuryHistoryAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.InjuryHistory;
}
