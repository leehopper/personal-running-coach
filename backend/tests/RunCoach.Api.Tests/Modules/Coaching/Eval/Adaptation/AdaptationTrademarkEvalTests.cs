using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Trademark guard for the adaptation surface (Slice 3 Unit 6 / DEC-043): the
/// adaptation prompt YAML and every coach-authored adaptation string must use
/// "Daniels-Gilbert zones" / "pace-zone index" and never the trademarked
/// four-letter pace-index term. Deterministic — no LLM. Complements the existing
/// <c>ContextAssemblerTests</c> parameterized Theory that guards the assembled
/// plan-generation prompt.
/// </summary>
[Trait("Category", "Eval")]
public sealed class AdaptationTrademarkEvalTests
{
    private const string TrademarkedTerm = "VDOT";

    public static TheoryData<string, string> CoachAuthoredStrings => new()
    {
        { nameof(CrisisResponseContent), CrisisResponseContent.CrisisResponse },
        { nameof(EmergencyResponseContent), EmergencyResponseContent.EmergencyResponse },
        { "AmberInjuryReferral", AmberReferralContent.InjuryReferral },
        { "AmberRedSReferral", AmberReferralContent.RedSReferral },
    };

    [Fact]
    public void AdaptationPrompt_UsesPaceZoneTerminologyAndNeverTheTrademarkedTerm()
    {
        // Arrange
        var promptPath = Path.Combine(EvalTestBase.GetPromptsDirectory(), "adaptation.v1.yaml");
        var prompt = File.ReadAllText(promptPath);

        // Assert
        prompt.Should().NotContainEquivalentOf(
            TrademarkedTerm,
            because: "the adaptation prompt must use 'Daniels-Gilbert zones' / 'pace-zone index' (DEC-043)");
        prompt.Should().ContainAny("Daniels-Gilbert", "pace-zone");
    }

    [Theory]
    [MemberData(nameof(CoachAuthoredStrings))]
    public void CoachAuthoredAdaptationString_NeverContainsTheTrademarkedTerm(string label, string content)
    {
        // Assert — every scripted safety string is trademark-clean.
        content.Should().NotContainEquivalentOf(
            TrademarkedTerm,
            because: $"coach-authored string '{label}' must not contain the trademarked pace-index term");
    }
}
