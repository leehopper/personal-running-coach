using FluentAssertions;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;
using RunCoach.Api.Tests.Modules.Training.Profiles;
using static RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutType;
using static RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation.DeviationIntent;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Deterministic classification calibration eval (Slice 3 Unit 6): drives each
/// catalog scenario through the real <see cref="DeviationEngine"/> →
/// <see cref="EscalationClassifier"/> chain (grounded in each profile's
/// Daniels-Gilbert zones) and asserts the resolved level matches the scenario's
/// physiologically-correct ground truth. An under-reaction is the dangerous,
/// suite-failing case (DEC-079); this catches a mis-tuned threshold constant
/// against the five <c>TestProfiles</c>. No LLM, no cache — always runs.
/// </summary>
[Trait("Category", "Eval")]
public sealed class AdaptationClassificationEvalTests
{
    public static TheoryData<string> ClassificationScenarioIds =>
        [.. AdaptationScenarioLibrary.ClassificationScenarios.Select(s => s.Id)];

    [Theory]
    [MemberData(nameof(ClassificationScenarioIds))]
    public void Classification_ResolvesGroundTruthLevel(string scenarioId)
    {
        // Arrange
        var scenario = AdaptationScenarioLibrary.ClassificationScenarios.Single(s => s.Id == scenarioId);
        var profile = EvalTestBase.LoadProfile(scenario.ProfileName);

        // Act
        var run = EscalationScenarioRunner.Run(profile, scenario);
        var score = AsymmetricEscalationScorer.Score(scenario.ExpectedLevel, run.FinalLevel);

        // Assert — exact match: an under-reaction is dangerous, an over-reaction
        // violates the no-upgrade guarantee. Either is a calibration miss.
        score.Outcome.Should().Be(
            EscalationScoreOutcome.Match,
            because: $"{scenario.Id}: expected {scenario.ExpectedLevel} but resolved {run.FinalLevel} (per-step: {string.Join(", ", run.StepLevels)})");
    }

    [Fact]
    public void RapidFireDeviations_FireExactlyOneRestructurePerCrossing()
    {
        // Arrange — five consecutive under-performances. The third crosses the
        // restructure threshold; the cooldown/dead-zone must then absorb the
        // rest rather than re-firing a restructure on every subsequent log.
        var profile = EvalTestBase.LoadProfile("lee");
        var scenario = new EscalationScenario(
            "rapidfire.lee.sustained-decline",
            AdaptationEvalCategory.Restructure,
            "lee",
            SafetyTier.Green,
            EscalationLevel.Restructure,
            [
                new EscalationScenarioStep(Easy, MinorSlow, 0),
                new EscalationScenarioStep(Easy, MinorSlow, 2),
                new EscalationScenarioStep(Easy, MinorSlow, 4),
                new EscalationScenarioStep(Easy, MinorSlow, 6),
                new EscalationScenarioStep(Easy, MinorSlow, 8),
            ]);

        // Act
        var run = EscalationScenarioRunner.Run(profile, scenario);

        // Assert — exactly one restructure across the whole sequence; the
        // dead-zone logs after the crossing do not re-fire it.
        var restructureCount = run.StepLevels.Count(level => level == EscalationLevel.Restructure);
        restructureCount.Should().Be(
            1,
            because: $"a restructure fires once per crossing, then the dead-zone holds (per-step: {string.Join(", ", run.StepLevels)})");
        run.FinalLevel.Should().Be(
            EscalationLevel.Absorb,
            because: "logs landing inside the post-restructure cooldown are absorbed");
    }
}
