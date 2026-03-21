using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Experiments;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Tests for <see cref="ExperimentContextAssembler"/> verifying parameterized
/// assembly with different token budgets, section orderings, and summarization modes.
/// </summary>
public class ExperimentContextAssemblerTests
{
    private readonly ExperimentContextAssembler _sut = new();

    [Fact]
    public void Assemble_8KBudget_StaysUnder8KTokens()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 3);
        var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-8k");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            8_000,
            because: "the 8K budget variation must stay under 8K tokens");
    }

    [Fact]
    public void Assemble_12KBudget_StaysUnder12KTokens()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 5);
        var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-12k");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            12_000,
            because: "the 12K budget variation must stay under 12K tokens");
    }

    [Fact]
    public void Assemble_15KBudget_StaysUnder15KTokens()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 10);
        var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-15k");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            15_000,
            because: "the 15K budget variation must stay under 15K tokens");
    }

    [Fact]
    public void Assemble_8KBudget_HasFewerTokensThan15K()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 5);
        var config8k = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-8k");
        var config15k = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-15k");

        // Act
        var result8k = _sut.Assemble(input, config8k);
        var result15k = _sut.Assemble(input, config15k);

        // Assert
        result8k.EstimatedTokenCount.Should().BeLessThan(
            result15k.EstimatedTokenCount,
            because: "the 8K variation should use fewer tokens than the 15K variation");
    }

    [Fact]
    public void Assemble_ProfileAtStart_HasProfileInStartSections()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == "position-start");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.StartSections.Should().Contain(s => s.Key == "user_profile");
        actual.StartSections.Should().Contain(s => s.Key == "goal_state");
        actual.StartSections.Should().Contain(s => s.Key == "fitness_estimate");
    }

    [Fact]
    public void Assemble_ProfileAtMiddle_HasProfileInMiddleSections()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == "position-middle");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.MiddleSections.Should().Contain(s => s.Key == "user_profile");
        actual.MiddleSections.Should().Contain(s => s.Key == "goal_state");
        actual.MiddleSections.Should().Contain(s => s.Key == "fitness_estimate");
        actual.StartSections.Should().NotContain(s => s.Key == "user_profile");
    }

    [Fact]
    public void Assemble_ProfileAtEnd_HasProfileInEndSections()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == "position-end");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EndSections.Should().Contain(s => s.Key == "user_profile");
        actual.EndSections.Should().Contain(s => s.Key == "goal_state");
        actual.EndSections.Should().Contain(s => s.Key == "fitness_estimate");
        actual.StartSections.Should().NotContain(s => s.Key == "user_profile");
    }

    [Fact]
    public void Assemble_ProfileAtMiddle_TrainingPacesRemainInStart()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == "position-middle");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.StartSections.Should().Contain(s => s.Key == "training_paces");
    }

    [Fact]
    public void Assemble_ProfileAtEnd_CurrentUserMessageStillPresent()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.PositionalPlacement.First(v => v.VariationId == "position-end");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EndSections.Should().Contain(s => s.Key == "current_user_message");
    }

    [Fact]
    public void Assemble_WeeklySummaryOnly_TrainingHistoryContainsWeekOf()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == "summarize-weekly");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        var historySection = actual.MiddleSections.FirstOrDefault(s => s.Key == "training_history");
        historySection.Should().NotBeNull();
        historySection!.Content.Should().Contain("Week of");
    }

    [Fact]
    public void Assemble_PerWorkoutOnly_TrainingHistoryContainsWorkoutDetails()
    {
        // Arrange
        var input = BuildLeeInput();
        var config = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == "summarize-per-workout");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        var historySection = actual.MiddleSections.FirstOrDefault(s => s.Key == "training_history");
        historySection.Should().NotBeNull();
        historySection!.Content.Should().Contain("/km");
    }

    [Fact]
    public void Assemble_WeeklySummaryOnly_UsesFewerTokensThanPerWorkout()
    {
        // Arrange
        var input = BuildLeeInput();
        var configWeekly = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == "summarize-weekly");
        var configPerWorkout = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == "summarize-per-workout");

        // Act
        var resultWeekly = _sut.Assemble(input, configWeekly);
        var resultPerWorkout = _sut.Assemble(input, configPerWorkout);

        // Assert
        var weeklyHistoryTokens = resultWeekly.MiddleSections
            .Where(s => s.Key == "training_history")
            .Sum(s => s.EstimatedTokens);
        var perWorkoutHistoryTokens = resultPerWorkout.MiddleSections
            .Where(s => s.Key == "training_history")
            .Sum(s => s.EstimatedTokens);

        weeklyHistoryTokens.Should().BeLessThan(
            perWorkoutHistoryTokens,
            because: "weekly summaries should use fewer tokens than per-workout detail");
    }

    [Fact]
    public void Assemble_NoHistory_SummarizationModeDoesNotCrash()
    {
        // Arrange — Sarah has no training history
        var sarah = TestProfiles.Sarah();
        var input = new ContextAssemblerInput(
            sarah.UserProfile,
            sarah.GoalState,
            sarah.GoalState.CurrentFitnessEstimate,
            sarah.GoalState.CurrentFitnessEstimate.TrainingPaces,
            sarah.TrainingHistory,
            ImmutableArray<ConversationTurn>.Empty,
            "Create a plan for my 5K.");
        var config = ExperimentVariations.SummarizationLevel.First(v => v.VariationId == "summarize-weekly");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.Should().NotBeNull();
        actual.EstimatedTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Assemble_ZeroConversationTurns_NoConversationSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 5);
        var config = ExperimentVariations.ConversationHistory.First(v => v.VariationId == "conversation-0");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EndSections.Should().NotContain(s => s.Key == "conversation_history");
    }

    [Fact]
    public void Assemble_FiveConversationTurns_HasConversationSection()
    {
        // Arrange
        var input = BuildLeeInputWithSampleConversation(5);
        var config = ExperimentVariations.ConversationHistory.First(v => v.VariationId == "conversation-5");

        // Act
        var actual = _sut.Assemble(input, config);

        // Assert
        actual.EndSections.Should().Contain(s => s.Key == "conversation_history");
    }

    [Theory]
    [InlineData("lee")]
    [InlineData("sarah")]
    [InlineData("maria")]
    public void Assemble_AllTokenBudgets_StayWithinBudget(string profileName)
    {
        // Arrange
        var profile = TestProfiles.All[profileName];
        var input = new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            SampleConversations.GetIntermediateTurns(5),
            "Create a training plan.");

        // Act & Assert
        foreach (var config in ExperimentVariations.TokenBudget)
        {
            var actual = _sut.Assemble(input, config);
            var reason = $"profile '{profileName}' with variation '{config.VariationId}' must stay within {config.TotalTokenBudget} token budget";
            actual.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
                config.TotalTokenBudget,
                because: reason);
        }
    }

    [Fact]
    public void Assemble_AllVariations_SystemPromptAlwaysPresent()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act & Assert
        foreach (var config in ExperimentVariations.All)
        {
            var actual = _sut.Assemble(input, config);
            actual.SystemPrompt.Should().NotBeNullOrWhiteSpace(
                because: $"variation '{config.VariationId}' must always include a system prompt");
        }
    }

    [Fact]
    public void Assemble_AllVariations_CurrentUserMessageAlwaysPresent()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act & Assert
        foreach (var config in ExperimentVariations.All)
        {
            var actual = _sut.Assemble(input, config);
            actual.EndSections.Should().Contain(
                s => s.Key == "current_user_message",
                because: $"variation '{config.VariationId}' must always include the current user message");
        }
    }

    private static ContextAssemblerInput BuildLeeInput(int conversationTurns = 0)
    {
        var lee = TestProfiles.Lee();
        var conversation = conversationTurns > 0
            ? BuildSimpleConversation(conversationTurns)
            : ImmutableArray<ConversationTurn>.Empty;

        return new ContextAssemblerInput(
            lee.UserProfile,
            lee.GoalState,
            lee.GoalState.CurrentFitnessEstimate,
            lee.GoalState.CurrentFitnessEstimate.TrainingPaces,
            lee.TrainingHistory,
            conversation,
            "Create a training plan for my half marathon.");
    }

    private static ContextAssemblerInput BuildLeeInputWithSampleConversation(int turns)
    {
        var lee = TestProfiles.Lee();
        var conversation = SampleConversations.GetIntermediateTurns(turns);

        return new ContextAssemblerInput(
            lee.UserProfile,
            lee.GoalState,
            lee.GoalState.CurrentFitnessEstimate,
            lee.GoalState.CurrentFitnessEstimate.TrainingPaces,
            lee.TrainingHistory,
            conversation,
            "Create a training plan for my half marathon.");
    }

    private static ImmutableArray<ConversationTurn> BuildSimpleConversation(int turns)
    {
        var builder = ImmutableArray.CreateBuilder<ConversationTurn>(turns);
        for (var i = 1; i <= turns; i++)
        {
            builder.Add(new ConversationTurn(
                $"User message {i}: How should I approach my training this week?",
                $"Coach response {i}: Based on your recent training, I'd suggest focusing on easy runs with one quality session."));
        }

        return builder.ToImmutable();
    }
}
