namespace RunCoach.Api.Modules.Coaching.Onboarding;

/// <summary>
/// Discriminated-union representation of a successfully extracted onboarding answer.
/// Produced from <see cref="ExtractedAnswer"/> by <see cref="ExtractedAnswer.ToUnion"/>
/// after the runtime guard validates the Pattern-B oneOf invariant. Downstream consumers
/// (projections, completion gate, regenerate handler) work against this union so the
/// "exactly one non-null slot matches Topic" invariant cannot be violated by a typed value.
/// </summary>
public abstract record AnswerExtraction(double Confidence)
{
    /// <summary>Gets the topic this extraction applies to.</summary>
    public abstract OnboardingTopic Topic { get; }
}
