using RunCoach.Api.Modules.Training.Plan;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Post-deserialization validator for <see cref="MacroPlanOutput"/> (F3). Enforces what
/// Anthropic constrained decoding cannot express:
/// <list type="number">
/// <item>internal consistency — phase week counts must sum to <see cref="MacroPlanOutput.TotalWeeks"/>;</item>
/// <item>event anchoring — when the <see cref="PlanHorizon"/> is anchored, the total weeks must
/// place race week inside the final phase's last week, tolerance ±1 week.</item>
/// </list>
/// Pure: it does not retry, log, or throw (beyond the null guard) — callers decide policy.
/// Mirrors <c>PlanAdaptationOutputValidator</c>.
/// </summary>
public static class MacroPlanOutputValidator
{
    /// <summary>Tolerance, in weeks, between the proposed total weeks and the target horizon.</summary>
    public const int HorizonToleranceWeeks = 1;

    /// <summary>Validates the macro against the computed horizon.</summary>
    /// <param name="output">The deserialized macro plan output.</param>
    /// <param name="horizon">The deterministic horizon decision for this generation.</param>
    public static MacroPlanOutputValidationResult Validate(MacroPlanOutput output, PlanHorizon horizon)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(horizon);

        // (1) Internal consistency — phases must sum to TotalWeeks. Always checked.
        var phaseWeekSum = 0;
        foreach (var phase in output.Phases)
        {
            phaseWeekSum += phase.Weeks;
        }

        if (phaseWeekSum != output.TotalWeeks)
        {
            return MacroPlanOutputValidationResult.Invalid(
                MacroPlanOutputValidationViolation.PhaseSumMismatch);
        }

        // (2) Event anchoring — only when a future event anchors the horizon.
        if (horizon.IsAnchored)
        {
            var delta = Math.Abs(output.TotalWeeks - horizon.TargetTotalWeeks!.Value);
            if (delta > HorizonToleranceWeeks)
            {
                return MacroPlanOutputValidationResult.Invalid(
                    MacroPlanOutputValidationViolation.HorizonMismatch);
            }
        }

        return MacroPlanOutputValidationResult.Valid();
    }
}
