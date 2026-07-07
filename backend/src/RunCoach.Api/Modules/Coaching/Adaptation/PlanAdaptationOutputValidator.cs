using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Post-deserialization validator enforcing the invariants Anthropic constrained decoding
/// cannot express on <see cref="PlanAdaptationOutput"/> (DEC-058 / DEC-079), in order:
/// <list type="number">
/// <item>at most one typed slot is non-null;</item>
/// <item>the populated slot (or its absence, for absorb) matches the
/// <see cref="AdaptationKind"/> discriminator;</item>
/// <item>GATE-BEFORE-INCREASE — any non-Green <see cref="SafetyTier"/> forbids a positive
/// <see cref="PlanAdaptationOutput.NetLoadDelta"/>;</item>
/// <item>a load-reducing restructure must include a forward path / return trajectory.</item>
/// </list>
/// Pure: it does not retry, log, or throw (beyond the null guard) — callers decide policy.
/// Mirrors <c>MessageIntentOutputValidator</c>.
/// </summary>
public static class PlanAdaptationOutputValidator
{
    /// <summary>
    /// Validates the structural and safety invariants against a deserialized adaptation output.
    /// </summary>
    /// <param name="output">The deserialized LLM adaptation output.</param>
    /// <returns>The validation result.</returns>
    public static PlanAdaptationOutputValidationResult Validate(PlanAdaptationOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var hasNudge = output.NudgePatch is not null;
        var hasRestructure = output.RestructurePlan is not null;

        // (1) At most one slot may be filled.
        if (hasNudge && hasRestructure)
        {
            return PlanAdaptationOutputValidationResult.Invalid(
                PlanAdaptationOutputValidationViolation.MultipleSlots);
        }

        // (2) The populated slot (or its absence) must match the discriminator.
        var slotMatchesKind = output.AdaptationKind switch
        {
            AdaptationKind.Absorb => !hasNudge && !hasRestructure,
            AdaptationKind.Nudge => hasNudge,
            AdaptationKind.Restructure => hasRestructure,
            _ => false,
        };

        if (!slotMatchesKind)
        {
            return PlanAdaptationOutputValidationResult.Invalid(
                PlanAdaptationOutputValidationViolation.SlotKindMismatch);
        }

        // (3) GATE-BEFORE-INCREASE: a non-Green safety tier forbids a load increase.
        if (output.SafetyTier != SafetyTier.Green && output.NetLoadDelta > 0)
        {
            return PlanAdaptationOutputValidationResult.Invalid(
                PlanAdaptationOutputValidationViolation.LoadIncreaseUnderNonGreenTier);
        }

        // (4) A load-reducing restructure must show the path back.
        if (output.AdaptationKind == AdaptationKind.Restructure
            && output.NetLoadDelta < 0
            && string.IsNullOrWhiteSpace(output.RestructurePlan!.ForwardPath))
        {
            return PlanAdaptationOutputValidationResult.Invalid(
                PlanAdaptationOutputValidationViolation.LoadReducingRestructureMissingForwardPath);
        }

        return PlanAdaptationOutputValidationResult.Valid();
    }
}
