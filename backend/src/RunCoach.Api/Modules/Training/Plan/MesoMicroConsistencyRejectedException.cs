using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Thrown when the week-1 micro workouts <em>still</em> fail deterministic meso/micro consistency
/// validation (<see cref="MesoMicroConsistencyValidator"/>) after the bounded corrective-hint retry
/// budget (<c>CoachingLlmSettings.MicroValidationMaxRetries</c>) is exhausted — the generation is
/// then terminally rejected with nothing staged (F-LIVE-2 / DEC-088). Sibling to
/// <see cref="PlanGenerationRejectedException"/> (macro-arithmetic rejection): both are
/// <em>expected</em> rejections of a well-formed LLM response, not transport/SDK failures, and both
/// leave the caller's Marten transaction to abort with zero events. User-facing callers (the
/// onboarding completion path) map it to the same terminal error envelope (422) as the macro
/// rejection; callers that don't map it propagate it through the standard error middleware.
/// </summary>
public sealed class MesoMicroConsistencyRejectedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MesoMicroConsistencyRejectedException"/> class.</summary>
    /// <param name="violation">The meso/micro consistency violation that triggered the rejection.</param>
    public MesoMicroConsistencyRejectedException(MesoMicroConsistencyViolation violation)
        : base($"Generated week-1 workouts were rejected by meso/micro consistency validation: {violation}.")
    {
        Violation = violation;
    }

    /// <summary>Gets the violation that triggered the rejection.</summary>
    public MesoMicroConsistencyViolation Violation { get; }
}
