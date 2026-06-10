using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Eval-side deterministic constraint evaluator for a restructure proposal,
/// mirroring the existing eval-side <c>PlanConstraintEvaluator</c> used for plan
/// generation. It checks the programming guardrails the prompt states but
/// Anthropic constrained decoding cannot enforce and the post-deserialization
/// validator deliberately does not police (DEC-079 keeps the validator to the
/// safety-critical invariants). Specifically: a revised weekly target must not
/// jump more than ~10% above the prior week — except while rebuilding toward
/// volume the athlete recently held. A restructure cuts the current week below
/// the pre-restructure baseline, and ramping back up to (but not past) that
/// recently-held volume is re-acclimatization, not novel load, so those weeks
/// are exempt from the +10% rate limit; only growth beyond the baseline is
/// rate-limited. Returns a list of human-readable violations (empty =
/// compliant), the same shape the plan-gen evaluator uses.
/// </summary>
internal static class AdaptationConstraintEvaluator
{
    /// <summary>The maximum tolerated week-over-week volume increase (10%) for novel load.</summary>
    internal const double MaxWeeklyVolumeJumpFactor = 1.10;

    /// <summary>
    /// Evaluates a restructure plan's revised weekly targets for week-over-week
    /// volume jumps beyond the allowed ceiling: the greater of +10% over the
    /// prior week and the recently-held <paramref name="baselineWeeklyKm"/>
    /// (the recovery-ramp exemption).
    /// </summary>
    /// <param name="plan">The restructure proposal to check.</param>
    /// <param name="baselineWeeklyKm">The weekly volume (km) the athlete held before the restructure cut.</param>
    /// <returns>The violations found (empty when compliant).</returns>
    internal static IReadOnlyList<string> Evaluate(RestructurePlan plan, int baselineWeeklyKm)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var violations = new List<string>();
        var previousKm = baselineWeeklyKm;

        foreach (var target in plan.RevisedWeeklyTargets.OrderBy(t => t.WeekNumber))
        {
            var ceiling = Math.Max(previousKm * MaxWeeklyVolumeJumpFactor, baselineWeeklyKm);
            if (target.WeeklyTargetKm > ceiling)
            {
                violations.Add(
                    $"Week {target.WeekNumber} jumps to {target.WeeklyTargetKm}km from {previousKm}km "
                    + $"(> {ceiling:0.#}km, the +10% ceiling; recovery ramps are exempt only up to "
                    + $"the recently-held {baselineWeeklyKm}km baseline).");
            }

            previousKm = target.WeeklyTargetKm;
        }

        return violations;
    }
}
