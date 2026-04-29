using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>Current-fitness slot of the <see cref="AnswerExtraction"/> discriminated union.</summary>
public sealed record CurrentFitnessExtraction(double Confidence, CurrentFitnessAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.CurrentFitness;
}
