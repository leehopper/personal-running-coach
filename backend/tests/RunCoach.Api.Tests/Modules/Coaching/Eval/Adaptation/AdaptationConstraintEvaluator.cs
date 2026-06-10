using RunCoach.Api.Modules.Coaching.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Eval-side deterministic constraint evaluator for a restructure proposal,
/// mirroring the production <c>PlanConstraintEvaluator</c> for plan generation.
/// It checks the programming guardrails the prompt states but Anthropic
/// constrained decoding cannot enforce and the post-deserialization validator
/// deliberately does not police (DEC-079 keeps the validator to the
/// safety-critical invariants). Specifically: a revised weekly target must not
/// jump more than ~10% above the prior week. Returns a list of human-readable
/// violations (empty = compliant), the same shape the plan-gen evaluator uses.
/// </summary>
internal static class AdaptationConstraintEvaluator
{
    /// <summary>The maximum tolerated week-over-week volume increase (10%).</summary>
    internal const double MaxWeeklyVolumeJumpFactor = 1.10;

    /// <summary>
    /// Evaluates a restructure plan's revised weekly targets for week-over-week
    /// volume jumps beyond the 10% ceiling, treating
    /// <paramref name="baselineWeeklyKm"/> as the week preceding the first revised week.
    /// </summary>
    /// <param name="plan">The restructure proposal to check.</param>
    /// <param name="baselineWeeklyKm">The current weekly volume (km) the first revised week builds on.</param>
    /// <returns>The violations found (empty when compliant).</returns>
    internal static IReadOnlyList<string> Evaluate(RestructurePlan plan, int baselineWeeklyKm)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var violations = new List<string>();
        var previousKm = baselineWeeklyKm;

        foreach (var target in plan.RevisedWeeklyTargets.OrderBy(t => t.WeekNumber))
        {
            var ceiling = previousKm * MaxWeeklyVolumeJumpFactor;
            if (target.WeeklyTargetKm > ceiling)
            {
                violations.Add(
                    $"Week {target.WeekNumber} jumps to {target.WeeklyTargetKm}km from {previousKm}km "
                    + $"(> {ceiling:0.#}km, the +10% ceiling).");
            }

            previousKm = target.WeeklyTargetKm;
        }

        return violations;
    }
}
