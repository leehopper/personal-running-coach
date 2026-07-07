using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Thrown when a generated macro plan <em>still</em> fails deterministic validation
/// (<see cref="MacroPlanOutputValidator"/>) after the bounded corrective-hint retry budget
/// (<c>CoachingLlmSettings.MacroValidationMaxRetries</c>) is exhausted — the generation is then
/// terminally rejected with nothing staged. DEC-087 amends the original DEC-073/DEC-080 "no
/// re-prompt" stance to a bounded re-prompt <em>before</em> this terminal throw; the
/// no-partial-commit half of that posture is unchanged. Distinct from
/// <c>CoachingLlmException</c>: this is an <em>expected</em> rejection of a well-formed LLM
/// response, not a transport/SDK failure. User-facing callers (the onboarding completion path)
/// are intended to map it to a terminal error envelope rather than a 5xx; callers that don't
/// map it propagate it through the standard error middleware.
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
