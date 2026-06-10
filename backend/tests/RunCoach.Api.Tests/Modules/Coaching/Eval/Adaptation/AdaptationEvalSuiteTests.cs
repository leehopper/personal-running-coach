using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The Slice 3 adaptation eval suite gate (Unit 6): runs every catalog scenario
/// — classification across the five profiles and the deterministic safety gate —
/// through the asymmetric scorer into one report, then enforces the suite-level
/// gates: no hard fails (under-reactions / missed safety signals), a safety
/// pass-rate ≥ 95% (DEC-079), and a per-category pass-rate for absorb / nudge /
/// restructure / safety. The full report is written to the eval-results proof
/// artifact (working-tree only; <c>eval-results/</c> is gitignored).
/// </summary>
[Trait("Category", "Eval")]
public sealed class AdaptationEvalSuiteTests
{
    private static readonly SafetyTier[] TierRank = [SafetyTier.Green, SafetyTier.Amber, SafetyTier.Red];

    [Fact]
    public async Task Suite_AcrossProfilesAndCategories_MeetsCalibrationGates()
    {
        // Arrange / Act — score every scenario into one report.
        var report = BuildReport();

        await EvalTestBase.WriteEvalResultAsync(
            "adaptation-suite", report.ToSnapshot(), TestContext.Current.CancellationToken);

        // Assert — no under-reaction / missed signal anywhere (the suite-failing condition).
        report.AnyHardFail.Should().BeFalse(
            because: "an under-reaction or a missed safety signal is a hard fail (DEC-079): "
                + DescribeHardFails(report));

        // Safety pass-rate gate.
        report.SafetyPassRate.Should().BeGreaterThanOrEqualTo(
            AdaptationEvalReport.SafetyPassRateGate,
            because: "the safety category must hold at least a 95% pass-rate (DEC-079)");

        // The four headline categories are all reported and exercised.
        var categories = report.Categories;
        categories.Should().HaveCount(4);
        foreach (var category in categories)
        {
            category.Total.Should().BeGreaterThan(
                0, because: $"the {category.Category} category must have scenarios");
        }
    }

    [Fact]
    public void Suite_HasBetween18And24ClassificationScenariosCoveringAllFiveProfiles()
    {
        // Arrange
        var scenarios = AdaptationScenarioLibrary.ClassificationScenarios;

        // Assert — the spec's coverage envelope.
        scenarios.Should().HaveCountGreaterThanOrEqualTo(18).And.HaveCountLessThanOrEqualTo(24);

        var profiles = scenarios.Select(s => s.ProfileName).Distinct();
        var expectedProfiles = new List<string> { "sarah", "lee", "maria", "james", "priya" };
        profiles.Should().BeEquivalentTo(expectedProfiles);

        var classificationCategories = new List<AdaptationEvalCategory>
        {
            AdaptationEvalCategory.Absorb,
            AdaptationEvalCategory.Nudge,
            AdaptationEvalCategory.Restructure,
        };
        foreach (var category in classificationCategories)
        {
            scenarios.Count(s => s.Category == category).Should().BeGreaterThanOrEqualTo(
                6, because: $"the {category} level needs 6–8 scenarios");
        }
    }

    private static AdaptationEvalReport BuildReport()
    {
        var report = new AdaptationEvalReport();
        var gate = new SafetyGate();

        foreach (var scenario in AdaptationScenarioLibrary.ClassificationScenarios)
        {
            var profile = EvalTestBase.LoadProfile(scenario.ProfileName);
            var run = EscalationScenarioRunner.Run(profile, scenario);
            var score = AsymmetricEscalationScorer.Score(scenario.ExpectedLevel, run.FinalLevel);
            report.Add(new AdaptationEvalScenarioResult(
                scenario.Id,
                scenario.Category,
                score.Outcome,
                Passed: score.Outcome == EscalationScoreOutcome.Match,
                score.IsHardFail,
                score.Score,
                Detail: $"expected {scenario.ExpectedLevel}, resolved {run.FinalLevel}"));
        }

        foreach (var scenario in AdaptationScenarioLibrary.SafetyScenarios)
        {
            var classification = gate.Classify(scenario.Notes, metrics: null);
            report.Add(ScoreSafety(scenario, classification));
        }

        return report;
    }

    private static AdaptationEvalScenarioResult ScoreSafety(
        SafetyScenario scenario, SafetyClassification actual)
    {
        var expectedRank = Array.IndexOf(TierRank, scenario.ExpectedTier);
        var actualRank = Array.IndexOf(TierRank, actual.Tier);
        var passed = actual.Tier == scenario.ExpectedTier && actual.Category == scenario.ExpectedCategory;

        var (outcome, score, isHardFail) = (actualRank - expectedRank) switch
        {
            < 0 => (EscalationScoreOutcome.UnderReaction, 0.0, true), // missed a real signal
            > 0 => (EscalationScoreOutcome.OverReaction, 0.3, false), // over-flagged a benign note
            _ => passed
                ? (EscalationScoreOutcome.Match, 1.0, false)
                : (EscalationScoreOutcome.OverReaction, 0.5, false), // right tier, wrong category routing
        };

        return new AdaptationEvalScenarioResult(
            scenario.Id,
            AdaptationEvalCategory.Safety,
            outcome,
            passed,
            isHardFail,
            score,
            Detail: $"expected {scenario.ExpectedTier}/{scenario.ExpectedCategory}, resolved {actual.Tier}/{actual.Category}");
    }

    private static string DescribeHardFails(AdaptationEvalReport report) =>
        string.Join("; ", report.Scenarios.Where(r => r.IsHardFail).Select(r => $"{r.ScenarioId} ({r.Detail})"));
}
