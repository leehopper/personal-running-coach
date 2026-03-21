using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Experiments;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Tests for <see cref="ExperimentVariations"/> to verify all experiment
/// configurations are well-formed and cover the required scenarios.
/// </summary>
public class ExperimentVariationsTests
{
    [Fact]
    public void TokenBudget_HasThreeVariations()
    {
        // Act
        var variations = ExperimentVariations.TokenBudget;

        // Assert
        variations.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("token-8k", 8_000)]
    [InlineData("token-12k", 12_000)]
    [InlineData("token-15k", 15_000)]
    public void TokenBudget_HasCorrectBudgets(string variationId, int expectedBudget)
    {
        // Act
        var variation = ExperimentVariations.TokenBudget.First(v => v.VariationId == variationId);

        // Assert
        variation.TotalTokenBudget.Should().Be(expectedBudget);
        variation.Category.Should().Be(ExperimentCategory.TokenBudget);
    }

    [Fact]
    public void PositionalPlacement_HasThreeVariations()
    {
        // Act
        var variations = ExperimentVariations.PositionalPlacement;

        // Assert
        variations.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("position-start", ProfilePlacement.Start)]
    [InlineData("position-middle", ProfilePlacement.Middle)]
    [InlineData("position-end", ProfilePlacement.End)]
    public void PositionalPlacement_HasCorrectPlacements(
        string variationId,
        ProfilePlacement expectedPlacement)
    {
        // Act
        var variation = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == variationId);

        // Assert
        variation.ProfilePlacement.Should().Be(expectedPlacement);
        variation.Category.Should().Be(ExperimentCategory.PositionalPlacement);
    }

    [Fact]
    public void SummarizationLevel_HasThreeVariations()
    {
        // Act
        var variations = ExperimentVariations.SummarizationLevel;

        // Assert
        variations.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("summarize-per-workout", SummarizationMode.PerWorkoutOnly)]
    [InlineData("summarize-weekly", SummarizationMode.WeeklySummaryOnly)]
    [InlineData("summarize-mixed", SummarizationMode.Mixed)]
    public void SummarizationLevel_HasCorrectModes(
        string variationId,
        SummarizationMode expectedMode)
    {
        // Act
        var variation = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == variationId);

        // Assert
        variation.SummarizationMode.Should().Be(expectedMode);
        variation.Category.Should().Be(ExperimentCategory.SummarizationLevel);
    }

    [Fact]
    public void ConversationHistory_HasTwoVariations()
    {
        // Act
        var variations = ExperimentVariations.ConversationHistory;

        // Assert
        variations.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("conversation-0", 0)]
    [InlineData("conversation-5", 5)]
    public void ConversationHistory_HasCorrectTurnCounts(
        string variationId,
        int expectedTurns)
    {
        // Act
        var variation = ExperimentVariations.ConversationHistory.First(v => v.VariationId == variationId);

        // Assert
        variation.ConversationTurns.Should().Be(expectedTurns);
        variation.Category.Should().Be(ExperimentCategory.ConversationHistory);
    }

    [Fact]
    public void All_ContainsAllVariations()
    {
        // Arrange
        var expectedCount = ExperimentVariations.TokenBudget.Length
            + ExperimentVariations.PositionalPlacement.Length
            + ExperimentVariations.SummarizationLevel.Length
            + ExperimentVariations.ConversationHistory.Length;

        // Act
        var all = ExperimentVariations.All;

        // Assert
        all.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void All_VariationIdsAreUnique()
    {
        // Act
        var ids = ExperimentVariations.All.Select(v => v.VariationId).ToList();

        // Assert
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_AllVariationsHaveDescriptions()
    {
        // Act & Assert
        foreach (var variation in ExperimentVariations.All)
        {
            variation.Description.Should().NotBeNullOrWhiteSpace(
                because: $"variation '{variation.VariationId}' must have a description");
        }
    }
}
