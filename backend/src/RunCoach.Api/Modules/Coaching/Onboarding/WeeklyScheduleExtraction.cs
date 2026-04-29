using RunCoach.Api.Modules.Coaching.Onboarding.Models;

namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Typed extraction result for the <see cref="OnboardingTopic.WeeklySchedule"/> topic.
/// </summary>
public sealed record WeeklyScheduleExtraction(double Confidence, WeeklyScheduleAnswer Value) : AnswerExtraction(Confidence)
{
    /// <inheritdoc/>
    public override OnboardingTopic Topic => OnboardingTopic.WeeklySchedule;
}
