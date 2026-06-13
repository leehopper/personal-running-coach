using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Thrown when a generated macro plan fails deterministic validation
/// (<see cref="MacroPlanOutputValidator"/>) — the generation is terminally rejected with
/// nothing staged (DEC-073/DEC-080 posture: no re-prompt, no partial commit). Distinct from
/// <c>CoachingLlmException</c>: this is an <em>expected</em> rejection of a well-formed LLM
/// response, not a transport/SDK failure, so it is surfaced as an HTTP-200 error envelope
/// rather than a 5xx.
/// </summary>
public sealed class PlanGenerationRejectedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PlanGenerationRejectedException"/> class.</summary>
    /// <param name="violation">The macro validation violation that triggered the rejection.</param>
    public PlanGenerationRejectedException(MacroPlanOutputValidationViolation violation)
        : base($"Generated macro plan was rejected by validation: {violation}.")
    {
        Violation = violation;
    }

    /// <summary>Gets the violation that triggered the rejection.</summary>
    public MacroPlanOutputValidationViolation Violation { get; }
}
