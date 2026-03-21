using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching;

public class ContextAssemblerTests
{
    private const int TokenBudget = 15_000;
    private readonly ContextAssembler _sut = new();

    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        // Arrange
        var text = string.Empty;

        // Act
        var actualTokens = _sut.EstimateTokens(text);

        // Assert
        actualTokens.Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        // Arrange
        string? text = null;

        // Act
        var actualTokens = _sut.EstimateTokens(text!);

        // Assert
        actualTokens.Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_KnownText_ProducesReasonableEstimate()
    {
        // Arrange — 100 characters should produce ~28 tokens (100/4 * 1.1 = 27.5, ceiling = 28)
        var text = new string('a', 100);
        var expectedTokens = 28;

        // Act
        var actualTokens = _sut.EstimateTokens(text);

        // Assert
        actualTokens.Should().Be(
            expectedTokens,
            because: "100 chars / 4 chars-per-token * 1.1 safety margin = 27.5, ceiling = 28");
    }

    [Fact]
    public void EstimateTokens_SingleCharacter_ReturnsOne()
    {
        // Arrange
        var text = "a";

        // Act
        var actualTokens = _sut.EstimateTokens(text);

        // Assert
        actualTokens.Should().Be(
            1,
            because: "1 char / 4 * 1.1 = 0.275, ceiling = 1");
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(40, 11)]
    [InlineData(400, 111)]
    [InlineData(1000, 275)]
    public void EstimateTokens_VariousLengths_MatchesCharacterRatioFormula(
        int charCount,
        int expectedTokens)
    {
        // Arrange
        var text = new string('x', charCount);

        // Act
        var actualTokens = _sut.EstimateTokens(text);

        // Assert
        actualTokens.Should().Be(expectedTokens);
    }

    [Fact]
    public void Assemble_CompleteProfileWithHistory_ContainsUserProfileInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "user_profile");
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("Lee");
        profileSection.Content.Should().Contain("34");
        profileSection.Content.Should().Contain("Male");
        profileSection.Content.Should().Contain("3 years");
        profileSection.Content.Should().Contain("40 km");
    }

    [Fact]
    public void Assemble_CompleteProfileWithHistory_ContainsGoalStateInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "goal_state");
        var goalSection = actualPrompt.StartSections.First(s => s.Key == "goal_state");
        goalSection.Content.Should().Contain("RaceGoal");
        goalSection.Content.Should().Contain("Half-Marathon");
    }

    [Fact]
    public void Assemble_CompleteProfileWithHistory_ContainsFitnessEstimateInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "fitness_estimate");
        var fitnessSection = actualPrompt.StartSections.First(s => s.Key == "fitness_estimate");
        fitnessSection.Content.Should().Contain("VDOT");
        fitnessSection.Content.Should().Contain("Intermediate");
    }

    [Fact]
    public void Assemble_CompleteProfileWithHistory_ContainsTrainingPacesInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "training_paces");
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().Contain("/km");
    }

    [Fact]
    public void Assemble_CompleteProfileWithHistory_HasFourStartSections()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert — user_profile, goal_state, fitness_estimate, training_paces
        actualPrompt.StartSections.Should().HaveCount(4);
    }

    [Fact]
    public void Assemble_WithHistoryAndConversation_TrainingHistoryInMiddleSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.MiddleSections.Should().Contain(s => s.Key == "training_history");
    }

    [Fact]
    public void Assemble_WithConversation_ConversationHistoryInEndSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.EndSections.Should().Contain(s => s.Key == "conversation_history");
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
    }

    [Fact]
    public void Assemble_WithConversation_CurrentUserMessageAlwaysInEndSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 0);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
        var msgSection = actualPrompt.EndSections.First(s => s.Key == "current_user_message");
        msgSection.Content.Should().Be("Create a training plan for my half marathon.");
    }

    [Fact]
    public void Assemble_CompleteProfile_NoPlaceholderValues()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert — no template markers or placeholder patterns should remain
        var allContent = string.Join("\n", actualPrompt.StartSections.Select(s => s.Content));
        allContent.Should().NotContain("{{");
        allContent.Should().NotContain("}}");
        allContent.Should().NotContain("{Name}");
        allContent.Should().NotContain("{Age}");
        allContent.Should().NotContain("TODO");
        allContent.Should().NotContain("PLACEHOLDER");
    }

    [Fact]
    public void Assemble_CompleteProfile_SystemPromptNotEmpty()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        actualPrompt.SystemPrompt.Should().Contain("running coach");
        actualPrompt.SystemPrompt.Should().Contain("SAFETY RULES");
        actualPrompt.SystemPrompt.Should().Contain("DETERMINISTIC GUARDRAILS");
    }

    [Fact]
    public void Assemble_MaxContent_StaysUnder15KTokens()
    {
        // Arrange — full profile with 4 weeks of per-workout history and 10 conversation turns
        // Maria has 4 weeks of history (24 workouts), the most of any profile.
        var input = BuildMariaInput(conversationTurns: 10);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "the assembled prompt must stay within the 15K token budget");
    }

    [Fact]
    public void Assemble_LeeProfileWith3WeeksAnd2Turns_StaysUnder15KTokens()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "Lee's profile with conversation should stay within budget");
    }

    [Fact]
    public void Assemble_AllProfilesWithMaxConversation_StayUnder15KTokens()
    {
        // Arrange & Act & Assert — every test profile should stay within budget
        foreach (var (name, profile) in TestProfiles.All)
        {
            var input = BuildInputFromProfile(profile, conversationTurns: 10);
            var actualPrompt = _sut.Assemble(input);

            actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
                TokenBudget,
                because: $"profile '{name}' with 10 conversation turns must stay within budget");
        }
    }

    [Fact]
    public void Assemble_BeginnerProfileNoHistory_TrainingHistorySectionEmpty()
    {
        // Arrange — Sarah is a beginner with no training history
        var input = BuildSarahInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.MiddleSections.Should().BeEmpty(
            because: "a beginner with no training history should have no middle sections");
    }

    [Fact]
    public void Assemble_BeginnerProfileNoHistory_PayloadIsValid()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert — should still have all start sections
        actualPrompt.StartSections.Should().HaveCount(4);
        actualPrompt.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
        actualPrompt.EstimatedTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Assemble_BeginnerProfile_FitnessEstimateShowsNoVdot()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var fitnessSection = actualPrompt.StartSections.First(s => s.Key == "fitness_estimate");
        fitnessSection.Content.Should().Contain("Not available");
        fitnessSection.Content.Should().Contain("Beginner");
    }

    [Fact]
    public void Assemble_BeginnerProfile_ProfileShowsNoRaces()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("Recent races: None");
    }

    [Fact]
    public void Assemble_InjuryProfile_IncludesInjuryHistory()
    {
        // Arrange
        var input = BuildJamesInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("Plantar fasciitis");
        profileSection.Content.Should().Contain("Active");
    }

    [Fact]
    public void Assemble_InjuryProfile_IncludesConstraints()
    {
        // Arrange
        var input = BuildJamesInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("20 minutes easy running only");
    }

    [Fact]
    public void Assemble_MaintenanceGoal_GoalTypeIsMaintenance()
    {
        // Arrange
        var input = BuildMariaInput(conversationTurns: 0);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var goalSection = actualPrompt.StartSections.First(s => s.Key == "goal_state");
        goalSection.Content.Should().Contain("Maintenance");
        goalSection.Content.Should().NotContain("Target race");
    }

    [Fact]
    public void Assemble_TokenEstimates_AllSectionsHavePositiveTokenCounts()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        foreach (var section in actualPrompt.StartSections)
        {
            section.EstimatedTokens.Should().BeGreaterThan(
                0,
                because: $"section '{section.Key}' should have a positive token estimate");
        }

        foreach (var section in actualPrompt.MiddleSections)
        {
            section.EstimatedTokens.Should().BeGreaterThan(
                0,
                because: $"section '{section.Key}' should have a positive token estimate");
        }

        foreach (var section in actualPrompt.EndSections)
        {
            section.EstimatedTokens.Should().BeGreaterThan(
                0,
                because: $"section '{section.Key}' should have a positive token estimate");
        }
    }

    [Fact]
    public void Assemble_TokenEstimateSum_MatchesReportedTotal()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var expectedTotal = _sut.EstimateTokens(actualPrompt.SystemPrompt)
            + actualPrompt.StartSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.MiddleSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.EndSections.Sum(s => s.EstimatedTokens);

        actualPrompt.EstimatedTokenCount.Should().Be(
            expectedTotal,
            because: "the reported total should match the sum of all section estimates");
    }

    [Fact]
    public void Assemble_ConstrainedProfile_IncludesScheduleConstraints()
    {
        // Arrange
        var priya = TestProfiles.Priya();
        var input = BuildInputFromProfile(priya, conversationTurns: 0);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("4 days/week max");
        profileSection.Content.Should().Contain("Never before 7:00 AM");
    }

    [Fact]
    public void Assemble_NoConversationHistory_NoConversationSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 0);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        actualPrompt.EndSections.Should().NotContain(s => s.Key == "conversation_history");
        actualPrompt.EndSections.Should().HaveCount(1);
    }

    [Fact]
    public void Assemble_ConversationHistory_ContainsUserAndCoachMessages()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var convSection = actualPrompt.EndSections.First(s => s.Key == "conversation_history");
        convSection.Content.Should().Contain("[User]:");
        convSection.Content.Should().Contain("[Coach]:");
    }

    [Fact]
    public void Assemble_ProfileWithRaceHistory_FormatsRaceTimes()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("10K in");
        profileSection.Content.Should().Contain("48:00");
    }

    [Fact]
    public void Assemble_ProfileWithAllPaces_FormatsAllPaceZones()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().Contain("Threshold pace");
    }

    [Fact]
    public void Assemble_BeginnerWithOnlyEasyPace_OnlyShowsEasyPace()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = _sut.Assemble(input);

        // Assert
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().NotContain("Marathon pace");
        pacesSection.Content.Should().NotContain("Threshold pace");
        pacesSection.Content.Should().NotContain("Interval pace");
        pacesSection.Content.Should().NotContain("Repetition pace");
    }

    private static ContextAssemblerInput BuildLeeInput(int conversationTurns = 0)
    {
        var lee = TestProfiles.Lee();
        return BuildInputFromProfile(lee, conversationTurns);
    }

    private static ContextAssemblerInput BuildSarahInput(int conversationTurns = 0)
    {
        var sarah = TestProfiles.Sarah();
        return BuildInputFromProfile(sarah, conversationTurns);
    }

    private static ContextAssemblerInput BuildMariaInput(int conversationTurns = 0)
    {
        var maria = TestProfiles.Maria();
        return BuildInputFromProfile(maria, conversationTurns);
    }

    private static ContextAssemblerInput BuildJamesInput(int conversationTurns = 0)
    {
        var james = TestProfiles.James();
        return BuildInputFromProfile(james, conversationTurns);
    }

    private static ContextAssemblerInput BuildInputFromProfile(
        TestProfile profile,
        int conversationTurns)
    {
        var conversation = BuildConversationHistory(conversationTurns);

        return new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            conversation,
            "Create a training plan for my half marathon.");
    }

    private static ImmutableArray<ConversationTurn> BuildConversationHistory(int turns)
    {
        if (turns == 0)
        {
            return ImmutableArray<ConversationTurn>.Empty;
        }

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
