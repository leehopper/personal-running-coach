using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>Target-event slot of the <see cref="AnswerExtraction"/> discriminated union.</summary>
public sealed record TargetEventExtraction(double Confidence, TargetEventAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.TargetEvent;
}
