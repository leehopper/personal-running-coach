using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Per-week context derived from the macro plan's phase list. Tells the
/// meso-tier LLM call which phase the week sits in and whether it is the
/// last week of a phase chunk that includes a deload week (the candidate
/// week for volume reduction).
/// </summary>
/// <param name="WeekIndex">1-based week number within the plan (1..4 in Slice 1).</param>
/// <param name="PhaseType">The periodization phase this week sits inside.</param>
/// <param name="IsDeloadCandidate">
/// Whether this week is the last week of a phase chunk that includes a
/// deload week — a hint to the LLM that this week may be the deload. The
/// LLM ultimately decides whether to flag the week with
/// <c>MesoWeekOutput.IsDeloadWeek = true</c>.
/// </param>
internal sealed record WeekContext(int WeekIndex, PhaseType PhaseType, bool IsDeloadCandidate)
{
    /// <summary>
    /// Derives the <see cref="WeekContext"/> for the given 1-based
    /// <paramref name="weekIndex"/> from the macro plan. Walks the phase
    /// list summing each phase's <c>Weeks</c> and returns the phase that
    /// owns the requested week. If the requested week falls past the last
    /// declared phase (defensive: macro might under-declare in pathological
    /// LLM outputs), the last phase is returned.
    /// </summary>
    /// <param name="macro">The macro plan output to derive context from.</param>
    /// <param name="weekIndex">1-based week index.</param>
    /// <returns>The week context.</returns>
    public static WeekContext FromMacro(MacroPlanOutput macro, int weekIndex)
    {
        ArgumentNullException.ThrowIfNull(macro);
        if (weekIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weekIndex), weekIndex, "Week index is 1-based.");
        }

        if (macro.Phases.Length == 0)
        {
            // No phases declared — emit a defensive Base/no-deload context
            // so the meso call still proceeds. The macro itself will fail
            // downstream validation if this happens; logging is the
            // caller's responsibility via the analyzer pipeline.
            return new WeekContext(weekIndex, PhaseType.Base, IsDeloadCandidate: false);
        }

        var cumulative = 0;
        foreach (var phase in macro.Phases)
        {
            var phaseEnd = cumulative + phase.Weeks;
            if (weekIndex <= phaseEnd)
            {
                var isLastWeekOfPhase = weekIndex == phaseEnd;
                return new WeekContext(
                    weekIndex,
                    phase.PhaseType,
                    IsDeloadCandidate: phase.IncludesDeload && isLastWeekOfPhase);
            }

            cumulative = phaseEnd;
        }

        var lastPhase = macro.Phases[^1];
        return new WeekContext(weekIndex, lastPhase.PhaseType, IsDeloadCandidate: false);
    }
}
