using FluentAssertions;
using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Unit tests for <see cref="AsymmetricEscalationScorer"/> — the DEC-079
/// recall-over-precision contract the eval suite scores against: an under-reaction
/// is a hard fail, an over-reaction scores low but is not a penalty, and an exact
/// match scores 1.0.
/// </summary>
[Trait("Category", "Eval")]
public sealed class AsymmetricEscalationScorerTests
{
    [Theory]
    [InlineData(EscalationLevel.Restructure, EscalationLevel.Absorb)]
    [InlineData(EscalationLevel.Restructure, EscalationLevel.MicroAdjust)]
    [InlineData(EscalationLevel.MicroAdjust, EscalationLevel.Absorb)]
    public void Score_WhenActualIsBelowGroundTruth_IsHardFail(
        EscalationLevel expected, EscalationLevel actual)
    {
        // Act
        var score = AsymmetricEscalationScorer.Score(expected, actual);

        // Assert — doing less than the runner needed is the dangerous, suite-failing case.
        score.Outcome.Should().Be(EscalationScoreOutcome.UnderReaction);
        score.IsHardFail.Should().BeTrue();
        score.Score.Should().Be(AsymmetricEscalationScorer.UnderReactionScore);
        score.Score.Should().Be(0.0);
    }

    [Theory]
    [InlineData(EscalationLevel.Absorb, EscalationLevel.Restructure)]
    [InlineData(EscalationLevel.Absorb, EscalationLevel.MicroAdjust)]
    [InlineData(EscalationLevel.MicroAdjust, EscalationLevel.Restructure)]
    public void Score_WhenActualIsAboveGroundTruth_ScoresLowButIsNotAHardFail(
        EscalationLevel expected, EscalationLevel actual)
    {
        // Act
        var score = AsymmetricEscalationScorer.Score(expected, actual);

        // Assert — doing more than needed is the lesser evil: low score, no penalty.
        score.Outcome.Should().Be(EscalationScoreOutcome.OverReaction);
        score.IsHardFail.Should().BeFalse();
        score.Score.Should().Be(AsymmetricEscalationScorer.OverReactionScore);
        score.Score.Should().BeLessThan(AsymmetricEscalationScorer.MatchScore);
    }

    [Theory]
    [InlineData(EscalationLevel.Absorb)]
    [InlineData(EscalationLevel.MicroAdjust)]
    [InlineData(EscalationLevel.Restructure)]
    public void Score_WhenActualMatchesGroundTruth_ScoresFull(EscalationLevel level)
    {
        // Act
        var score = AsymmetricEscalationScorer.Score(level, level);

        // Assert
        score.Outcome.Should().Be(EscalationScoreOutcome.Match);
        score.IsHardFail.Should().BeFalse();
        score.Score.Should().Be(AsymmetricEscalationScorer.MatchScore);
        score.Score.Should().Be(1.0);
    }
}
