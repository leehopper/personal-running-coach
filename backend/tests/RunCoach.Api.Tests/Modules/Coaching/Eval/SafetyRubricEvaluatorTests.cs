using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Models.Structured;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for <see cref="SafetyRubricEvaluator"/>.
/// Tests rubric construction and verdict parsing — no API calls.
/// </summary>
public class SafetyRubricEvaluatorTests
{
    private static readonly JsonSerializerOptions VerdictOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void BuildJudgePrompt_IncludesScenarioAndCriteria()
    {
        // Arrange
        var evaluator = new SafetyRubricEvaluator("Medical question scenario", SafetyRubrics.Medical);

        // Act
        var prompt = evaluator.BuildJudgePrompt("Please see your doctor about that knee pain.");

        // Assert
        prompt.Should().Contain("Medical question scenario");
        prompt.Should().Contain("see your doctor");
        prompt.Should().Contain("recommends_professional");
        prompt.Should().Contain("avoids_diagnosis");
        prompt.Should().Contain("avoids_treatment");
        prompt.Should().Contain("no_train_through_pain");
    }

    [Fact]
    public void BuildJudgeSystemPrompt_ContainsInstructions()
    {
        // Act
        var prompt = SafetyRubricEvaluator.BuildJudgeSystemPrompt();

        // Assert
        prompt.Should().Contain("safety evaluation judge");
        prompt.Should().Contain("overall_score");
        prompt.Should().Contain("evidence");
    }

    [Fact]
    public void SafetyVerdict_RoundTrip_DeserializesCorrectly()
    {
        // Arrange
        var verdict = new SafetyVerdict
        {
            Criteria =
            [
                new SafetyCriterionResult
                {
                    CriterionName = "recommends_professional",
                    Passed = true,
                    Evidence = "Coach said 'please consult your doctor'.",
                },
                new SafetyCriterionResult
                {
                    CriterionName = "avoids_diagnosis",
                    Passed = true,
                    Evidence = "No specific diagnosis was given.",
                },
            ],
            OverallScore = 1.0m,
            OverallReason = "All safety criteria pass.",
        };

        // Act
        var json = JsonSerializer.Serialize(verdict, VerdictOptions);
        var deserialized = JsonSerializer.Deserialize<SafetyVerdict>(json, VerdictOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.OverallScore.Should().Be(1.0m);
        deserialized.Criteria.Should().HaveCount(2);
        deserialized.Criteria[0].Passed.Should().BeTrue();
    }

    [Fact]
    public void SafetyVerdict_FailingCriteria_DeserializesCorrectly()
    {
        // Arrange — simulates a judge response with a failing criterion
        var json = """
            {
                "criteria": [
                    {
                        "criterion_name": "recommends_professional",
                        "passed": true,
                        "evidence": "Response includes 'see your doctor'."
                    },
                    {
                        "criterion_name": "avoids_diagnosis",
                        "passed": false,
                        "evidence": "Response diagnosed IT band syndrome."
                    }
                ],
                "overall_score": 0.0,
                "overall_reason": "Failed avoids_diagnosis criterion."
            }
            """;

        // Act
        var verdict = JsonSerializer.Deserialize<SafetyVerdict>(json, VerdictOptions);

        // Assert
        verdict.Should().NotBeNull();
        verdict!.OverallScore.Should().Be(0.0m);
        verdict.Criteria.Should().HaveCount(2);
        verdict.Criteria[1].Passed.Should().BeFalse();
        verdict.Criteria[1].CriterionName.Should().Be("avoids_diagnosis");
    }

    [Theory]
    [InlineData("Medical")]
    [InlineData("Overtraining")]
    [InlineData("Injury")]
    [InlineData("Crisis")]
    [InlineData("Nutrition")]
    public void SafetyRubrics_AllScenarios_HaveFourCriteria(string scenarioName)
    {
        // Arrange
        var criteria = scenarioName switch
        {
            "Medical" => SafetyRubrics.Medical,
            "Overtraining" => SafetyRubrics.Overtraining,
            "Injury" => SafetyRubrics.Injury,
            "Crisis" => SafetyRubrics.Crisis,
            "Nutrition" => SafetyRubrics.Nutrition,
            _ => throw new ArgumentException($"Unknown scenario: {scenarioName}"),
        };

        // Assert
        criteria.Should().HaveCount(4);
        criteria.Should().AllSatisfy(c =>
        {
            c.Name.Should().NotBeNullOrWhiteSpace();
            c.Description.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Constructor_NullScenario_Throws()
    {
        // Act
        var act = () => new SafetyRubricEvaluator(null!, SafetyRubrics.Medical);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullCriteria_Throws()
    {
        // Act
        var act = () => new SafetyRubricEvaluator("test", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
