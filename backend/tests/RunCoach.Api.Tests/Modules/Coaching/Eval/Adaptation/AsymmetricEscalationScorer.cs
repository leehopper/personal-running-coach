using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Scores a deterministic escalation result against a scenario's declared
/// ground-truth level with the DEC-079 asymmetry baked in: an under-reaction
/// (the engine did less than the runner needed) is a hard fail, while an
/// over-reaction (the engine did more than needed) scores low but is not a
/// penalty. The escalation ladder is ordinal (<see cref="EscalationLevel"/> is
/// explicitly 0-indexed), so the comparison is a simple rank compare.
/// </summary>
internal static class AsymmetricEscalationScorer
{
    /// <summary>Score for an exact match between resolved and ground-truth level.</summary>
    internal const double MatchScore = 1.0;

    /// <summary>Score for an over-reaction (resolved level above ground truth): low, not a penalty.</summary>
    internal const double OverReactionScore = 0.3;

    /// <summary>Score for an under-reaction (resolved level below ground truth): the hard-failure floor.</summary>
    internal const double UnderReactionScore = 0.0;

    /// <summary>
    /// Scores the <paramref name="actual"/> resolved level against the
    /// <paramref name="expected"/> ground-truth level.
    /// </summary>
    /// <param name="expected">The scenario's physiologically-correct ground-truth level.</param>
    /// <param name="actual">The level the deterministic engine resolved.</param>
    /// <returns>The asymmetric score, flagging an under-reaction as a hard fail.</returns>
    internal static EscalationScore Score(EscalationLevel expected, EscalationLevel actual)
    {
        var expectedRank = (int)expected;
        var actualRank = (int)actual;

        if (actualRank == expectedRank)
        {
            return new EscalationScore(EscalationScoreOutcome.Match, MatchScore, IsHardFail: false);
        }

        return actualRank < expectedRank
            ? new EscalationScore(EscalationScoreOutcome.UnderReaction, UnderReactionScore, IsHardFail: true)
            : new EscalationScore(EscalationScoreOutcome.OverReaction, OverReactionScore, IsHardFail: false);
    }
}
