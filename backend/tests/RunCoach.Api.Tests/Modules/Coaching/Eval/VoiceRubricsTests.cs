using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

public sealed class VoiceRubricsTests
{
    [Fact]
    public void Restraint_HasTheExpectedCriteria()
    {
        // Assert
        VoiceRubrics.Restraint.Select(c => c.Name).Should().BeEquivalentTo(
            "direct_register",
            "no_validation_opener",
            "no_filler_enthusiasm",
            "keeps_rationale",
            "offers_forward_path");
    }

    [Fact]
    public void BuildJudgePrompt_IncludesEveryRestraintCriterion()
    {
        // Arrange
        var evaluator = new SafetyRubricEvaluator("voice restraint check", VoiceRubrics.Restraint);

        // Act
        var prompt = evaluator.BuildJudgePrompt("Cut Sunday to 9 km. Volume rebuilds Monday.");

        // Assert
        foreach (var criterion in VoiceRubrics.Restraint)
        {
            prompt.Should().Contain(criterion.Name);
        }
    }
}
