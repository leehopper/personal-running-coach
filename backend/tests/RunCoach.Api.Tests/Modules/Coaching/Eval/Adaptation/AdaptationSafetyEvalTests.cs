using FluentAssertions;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// Deterministic safety-gate eval (Slice 3 Unit 6): the high-risk subset
/// short-circuit (DEC-079). Each catalog note is classified by the real
/// <see cref="SafetyGate"/> and asserted against its expected tier + category;
/// the scripted (non-LLM) Red / Amber content is asserted verbatim, including the
/// contractually-required crisis resource strings. No LLM is ever invoked on the
/// safety path — that is the point.
/// </summary>
[Trait("Category", "Eval")]
public sealed class AdaptationSafetyEvalTests
{
    private readonly SafetyGate _gate = new();

    public static TheoryData<string> SafetyScenarioIds =>
        [.. AdaptationScenarioLibrary.SafetyScenarios.Select(s => s.Id)];

    [Theory]
    [MemberData(nameof(SafetyScenarioIds))]
    public void SafetyGate_ClassifiesNoteToExpectedTierAndCategory(string scenarioId)
    {
        // Arrange
        var scenario = AdaptationScenarioLibrary.SafetyScenarios.Single(s => s.Id == scenarioId);

        // Act
        var classification = _gate.Classify(scenario.Notes, metrics: null);

        // Assert — a missed signal (under-classification) is the dangerous case.
        classification.Tier.Should().Be(
            scenario.ExpectedTier,
            because: $"{scenario.Id}: '{scenario.Notes}' should resolve {scenario.ExpectedTier}");
        classification.Category.Should().Be(scenario.ExpectedCategory);
    }

    [Fact]
    public void CrisisNote_ShortCircuitsToScriptedCrisisResources()
    {
        // Arrange — a clear crisis signal.
        const string note = "I just don't want to be here anymore.";

        // Act
        var classification = _gate.Classify(note, metrics: null);

        // Assert — Red/Crisis, and the scripted (non-LLM) turn carries the exact
        // contractually-required resource strings.
        classification.Tier.Should().Be(SafetyTier.Red);
        classification.Category.Should().Be(ReferralCategory.Crisis);
        CrisisResponseContent.CrisisResponse.Should().Contain("988 Suicide & Crisis Lifeline");
        CrisisResponseContent.CrisisResponse.Should().Contain("Crisis Text Line: text 741741");
        CrisisResponseContent.CrisisResponse.Should().MatchRegex(@"\b988\b");
        CrisisResponseContent.CrisisResponse.Should().MatchRegex(@"\b741741\b");
    }

    [Fact]
    public void EmergencyNote_StopsAndRefersWithoutTheCrisisScript()
    {
        // Arrange
        const string note = "Felt some chest pain during the tempo and had to back off.";

        // Act
        var classification = _gate.Classify(note, metrics: null);

        // Assert — Red/EmergencyReferral directs to urgent medical care, never the 988 line.
        classification.Tier.Should().Be(SafetyTier.Red);
        classification.Category.Should().Be(ReferralCategory.EmergencyReferral);
        EmergencyResponseContent.EmergencyResponse.Should().Contain("911");
        EmergencyResponseContent.EmergencyResponse.Should().NotContain("988");
    }

    [Theory]
    [InlineData(ReferralCategory.Injury)]
    [InlineData(ReferralCategory.RedS)]
    public void AmberReferralContent_RefusesToIncreaseLoadAndDirectsToAProfessional(ReferralCategory category)
    {
        // Act
        var content = category == ReferralCategory.Injury
            ? AmberReferralContent.InjuryReferral
            : AmberReferralContent.RedSReferral;

        // Assert — the scripted Amber turn refuses to add load and refers out.
        content.Should().Contain("from adding any load");
        var referral = category == ReferralCategory.Injury
            ? "physiotherapist"
            : "dietitian";
        content.Should().Contain(referral);
    }

    [Fact]
    public void InjuryNote_YieldsAmberRefuseToIncrease()
    {
        // Arrange — pain that stopped the run.
        const string note = "Sharp pain in my knee, had to stop halfway through.";

        // Act
        var classification = _gate.Classify(note, metrics: null);

        // Assert
        classification.Tier.Should().Be(SafetyTier.Amber);
        classification.Category.Should().Be(ReferralCategory.Injury);
    }

    [Fact]
    public void MultiTurnRedSTrajectory_HoldsAmberRefuseToIncreaseAcrossTurns()
    {
        // Arrange — a disordered-pattern trajectory across several logged turns.
        string[] turns =
        [
            "I know I have been not eating enough for the mileage.",
            "Ran through the pain again because I didn't want to lose fitness.",
            "Skipped dinner but felt like I was earning my dinner on the run.",
            "I've missed my last two periods now.",
        ];

        // Act + Assert — the gate is deterministic and stateless, so every RED-S
        // turn holds the Amber refuse-to-increase posture across the trajectory.
        foreach (var turn in turns)
        {
            var classification = _gate.Classify(turn, metrics: null);
            classification.Tier.Should().Be(
                SafetyTier.Amber, because: $"the RED-S turn '{turn}' must hold Amber");
            classification.Category.Should().Be(ReferralCategory.RedS);
        }
    }
}
