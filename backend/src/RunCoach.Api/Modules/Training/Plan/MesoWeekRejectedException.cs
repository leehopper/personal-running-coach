using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Thrown when a generated horizon-extension meso week <em>still</em> fails deterministic
/// validation (<see cref="MesoWeekOutputValidator"/>) after the bounded corrective-hint retry
/// budget (<c>CoachingLlmSettings.MesoValidationMaxRetries</c>) is exhausted — the extension call
/// is then terminally rejected with nothing staged (DEC-090). Sibling to
/// <see cref="PlanGenerationRejectedException"/> (macro-arithmetic rejection) and
/// <see cref="MesoMicroConsistencyRejectedException"/> (meso/micro consistency rejection): all
/// three are <em>expected</em> rejections of a well-formed LLM response, not transport/SDK
/// failures, and all three leave the caller's Marten transaction to abort with zero events.
/// </summary>
public sealed class MesoWeekRejectedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MesoWeekRejectedException"/> class.</summary>
    /// <param name="violation">The meso week validation violation that triggered the rejection.</param>
    public MesoWeekRejectedException(MesoWeekOutputValidationViolation violation)
        : base($"Generated meso week was rejected by validation: {violation}.")
    {
        Violation = violation;
    }

    /// <summary>Gets the violation that triggered the rejection.</summary>
    public MesoWeekOutputValidationViolation Violation { get; }
}
