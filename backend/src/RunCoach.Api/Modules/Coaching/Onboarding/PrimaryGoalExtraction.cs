using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>Primary-goal slot of the <see cref="AnswerExtraction"/> discriminated union.</summary>
public sealed record PrimaryGoalExtraction(double Confidence, PrimaryGoalAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.PrimaryGoal;
}
