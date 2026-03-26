using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Tests.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching;

public class ContextAssemblerTests
{
    private const int TokenBudget = 15_000;
    private readonly ContextAssembler _sut;

    public ContextAssemblerTests()
    {
        var store = CreateMockPromptStore();
        _sut = new ContextAssembler(store, NullLogger<ContextAssembler>.Instance);
    }

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
    public async Task AssembleAsync_CompleteProfileWithHistory_ContainsUserProfileInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

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
    public async Task AssembleAsync_CompleteProfileWithHistory_ContainsGoalStateInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "goal_state");
        var goalSection = actualPrompt.StartSections.First(s => s.Key == "goal_state");
        goalSection.Content.Should().Contain("RaceGoal");
        goalSection.Content.Should().Contain("Half-Marathon");
    }

    [Fact]
    public async Task AssembleAsync_CompleteProfileWithHistory_ContainsFitnessEstimateInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "fitness_estimate");
        var fitnessSection = actualPrompt.StartSections.First(s => s.Key == "fitness_estimate");
        fitnessSection.Content.Should().Contain("VDOT");
        fitnessSection.Content.Should().Contain("Intermediate");
    }

    [Fact]
    public async Task AssembleAsync_CompleteProfileWithHistory_ContainsTrainingPacesInStartSection()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.StartSections.Should().Contain(s => s.Key == "training_paces");
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().Contain("/km");
    }

    [Fact]
    public async Task AssembleAsync_CompleteProfileWithHistory_HasFourStartSections()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — user_profile, goal_state, fitness_estimate, training_paces
        actualPrompt.StartSections.Should().HaveCount(4);
    }

    [Fact]
    public async Task AssembleAsync_WithHistoryAndConversation_TrainingHistoryInMiddleSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.MiddleSections.Should().Contain(s => s.Key == "training_history");
    }

    [Fact]
    public async Task AssembleAsync_WithConversation_ConversationHistoryInEndSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EndSections.Should().Contain(s => s.Key == "conversation_history");
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
    }

    [Fact]
    public async Task AssembleAsync_WithConversation_CurrentUserMessageAlwaysInEndSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 0);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
        var msgSection = actualPrompt.EndSections.First(s => s.Key == "current_user_message");
        msgSection.Content.Should().Be("Create a training plan for my half marathon.");
    }

    [Fact]
    public async Task AssembleAsync_CompleteProfile_NoPlaceholderValues()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

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
    public async Task AssembleAsync_CompleteProfile_SystemPromptNotEmpty()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        actualPrompt.SystemPrompt.Should().Contain("running coach");
        actualPrompt.SystemPrompt.Should().Contain("SAFETY RULES");
        actualPrompt.SystemPrompt.Should().Contain("DETERMINISTIC GUARDRAILS");
    }

    [Fact]
    public async Task AssembleAsync_MaxContent_StaysUnder15KTokens()
    {
        // Arrange — full profile with 4 weeks of per-workout history and 10 conversation turns
        // Maria has 4 weeks of history (24 workouts), the most of any profile.
        var input = BuildMariaInput(conversationTurns: 10);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "the assembled prompt must stay within the 15K token budget");
    }

    [Fact]
    public async Task AssembleAsync_LeeProfileWith3WeeksAnd2Turns_StaysUnder15KTokens()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "Lee's profile with conversation should stay within budget");
    }

    [Fact]
    public async Task AssembleAsync_AllProfilesWithMaxConversation_StayUnder15KTokens()
    {
        // Arrange & Act & Assert — every test profile should stay within budget
        foreach (var (name, profile) in TestProfiles.All)
        {
            var input = BuildInputFromProfile(profile, conversationTurns: 10);
            var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

            actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
                TokenBudget,
                because: $"profile '{name}' with 10 conversation turns must stay within budget");
        }
    }

    [Fact]
    public async Task AssembleAsync_BeginnerProfileNoHistory_TrainingHistorySectionEmpty()
    {
        // Arrange — Sarah is a beginner with no training history
        var input = BuildSarahInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.MiddleSections.Should().BeEmpty(
            because: "a beginner with no training history should have no middle sections");
    }

    [Fact]
    public async Task AssembleAsync_BeginnerProfileNoHistory_PayloadIsValid()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — should still have all start sections
        actualPrompt.StartSections.Should().HaveCount(4);
        actualPrompt.SystemPrompt.Should().NotBeNullOrWhiteSpace();
        actualPrompt.EndSections.Should().Contain(s => s.Key == "current_user_message");
        actualPrompt.EstimatedTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssembleAsync_BeginnerProfile_FitnessEstimateShowsNoVdot()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var fitnessSection = actualPrompt.StartSections.First(s => s.Key == "fitness_estimate");
        fitnessSection.Content.Should().Contain("Not available");
        fitnessSection.Content.Should().Contain("Beginner");
    }

    [Fact]
    public async Task AssembleAsync_BeginnerProfile_ProfileShowsNoRaces()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("Recent races: None");
    }

    [Fact]
    public async Task AssembleAsync_InjuryProfile_IncludesInjuryHistory()
    {
        // Arrange
        var input = BuildJamesInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("Plantar fasciitis");
        profileSection.Content.Should().Contain("Active");
    }

    [Fact]
    public async Task AssembleAsync_InjuryProfile_IncludesConstraints()
    {
        // Arrange
        var input = BuildJamesInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("20 minutes easy running only");
    }

    [Fact]
    public async Task AssembleAsync_MaintenanceGoal_GoalTypeIsMaintenance()
    {
        // Arrange
        var input = BuildMariaInput(conversationTurns: 0);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var goalSection = actualPrompt.StartSections.First(s => s.Key == "goal_state");
        goalSection.Content.Should().Contain("Maintenance");
        goalSection.Content.Should().NotContain("Target race");
    }

    [Fact]
    public async Task AssembleAsync_TokenEstimates_AllSectionsHavePositiveTokenCounts()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

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
    public async Task AssembleAsync_TokenEstimateSum_MatchesReportedTotal()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

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
    public async Task AssembleAsync_ConstrainedProfile_IncludesScheduleConstraints()
    {
        // Arrange
        var priya = TestProfiles.Priya();
        var input = BuildInputFromProfile(priya, conversationTurns: 0);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("4 days/week max");
        profileSection.Content.Should().Contain("Never before 7:00 AM");
    }

    [Fact]
    public async Task AssembleAsync_NoConversationHistory_NoConversationSection()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 0);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EndSections.Should().NotContain(s => s.Key == "conversation_history");
        actualPrompt.EndSections.Should().HaveCount(1);
    }

    [Fact]
    public async Task AssembleAsync_ConversationHistory_ContainsUserAndCoachMessages()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var convSection = actualPrompt.EndSections.First(s => s.Key == "conversation_history");
        convSection.Content.Should().Contain("[User]:");
        convSection.Content.Should().Contain("[Coach]:");
    }

    [Fact]
    public async Task AssembleAsync_FifteenConversationTurns_TruncatesToLastTen()
    {
        // Arrange — 15 turns exceeds MaxConversationTurns (10), so only the last 10 should appear
        var input = BuildLeeInput(conversationTurns: 15);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — turns 6-15 should be present (the last 10), turns 1-5 should be truncated
        var convSection = actualPrompt.EndSections.First(s => s.Key == "conversation_history");

        // Truncated turns (1-5) must not appear
        for (var i = 1; i <= 5; i++)
        {
            convSection.Content.Should().NotContain(
                $"User message {i}:",
                because: $"turn {i} should be truncated when conversation exceeds {ContextAssembler.MaxConversationTurns} turns");
        }

        // Retained turns (6-15) must all appear
        for (var i = 6; i <= 15; i++)
        {
            convSection.Content.Should().Contain(
                $"User message {i}:",
                because: $"turn {i} should be retained as one of the most recent {ContextAssembler.MaxConversationTurns} turns");
        }

        // Count [User]: markers to confirm exactly 10 turns
        var actualTurnCount = convSection.Content
            .Split("[User]:").Length - 1;
        actualTurnCount.Should().Be(
            ContextAssembler.MaxConversationTurns,
            because: $"exactly {ContextAssembler.MaxConversationTurns} turns should remain after truncation");
    }

    [Fact]
    public async Task AssembleAsync_ProfileWithRaceHistory_FormatsRaceTimes()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var profileSection = actualPrompt.StartSections.First(s => s.Key == "user_profile");
        profileSection.Content.Should().Contain("10K in");
        profileSection.Content.Should().Contain("48:00");
    }

    [Fact]
    public async Task AssembleAsync_ProfileWithAllPaces_FormatsAllPaceZones()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().Contain("Threshold pace");
    }

    [Fact]
    public async Task AssembleAsync_BeginnerWithOnlyEasyPace_OnlyShowsEasyPace()
    {
        // Arrange
        var input = BuildSarahInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var pacesSection = actualPrompt.StartSections.First(s => s.Key == "training_paces");
        pacesSection.Content.Should().Contain("Easy pace");
        pacesSection.Content.Should().NotContain("Marathon pace");
        pacesSection.Content.Should().NotContain("Threshold pace");
        pacesSection.Content.Should().NotContain("Interval pace");
        pacesSection.Content.Should().NotContain("Repetition pace");
    }

    // ================================================================
    // YAML-based assembly tests (verify YAML system prompt behavior)
    // ================================================================
    [Fact]
    public async Task AssembleAsync_WithPromptStore_UsesYamlSystemPrompt()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — system prompt should come from YAML, not hardcoded constant
        actualPrompt.SystemPrompt.Should().Contain("evidence-based running coach");
        actualPrompt.SystemPrompt.Should().Contain("COMMUNICATION FRAMEWORK");
        actualPrompt.SystemPrompt.Should().Contain("SAFETY RULES");
        actualPrompt.SystemPrompt.Should().Contain("DETERMINISTIC GUARDRAILS");
        actualPrompt.SystemPrompt.Should().Contain("SEMANTIC OUTPUT GUIDANCE");
    }

    [Fact]
    public async Task AssembleAsync_WithPromptStore_StaticPrefixContainsZeroAthleteData()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — the system prompt (static prefix) must contain NO athlete-specific data
        actualPrompt.SystemPrompt.Should().NotContain("Lee");
        actualPrompt.SystemPrompt.Should().NotContain("34");
        actualPrompt.SystemPrompt.Should().NotContain("Half-Marathon");
        actualPrompt.SystemPrompt.Should().NotContain("VDOT");
        actualPrompt.SystemPrompt.Should().NotContain("Easy pace:");
        actualPrompt.SystemPrompt.Should().NotContain("40 km");
    }

    [Fact]
    public async Task AssembleAsync_WithPromptStore_DynamicSectionsContainAthleteData()
    {
        // Arrange
        var input = BuildLeeInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — athlete data should be in the start/middle/end sections
        var allSectionContent = string.Join(
            "\n",
            actualPrompt.StartSections.Select(s => s.Content)
                .Concat(actualPrompt.MiddleSections.Select(s => s.Content))
                .Concat(actualPrompt.EndSections.Select(s => s.Content)));

        allSectionContent.Should().Contain("Lee");
        allSectionContent.Should().Contain("40 km");
        allSectionContent.Should().Contain("Easy pace");
    }

    [Fact]
    public async Task AssembleAsync_WithPromptStore_StaysUnder15KTokens()
    {
        // Arrange
        var input = BuildMariaInput(conversationTurns: 10);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "the assembled prompt must stay within the 15K token budget with YAML prompts");
    }

    [Fact]
    public async Task AssembleAsync_WithPromptStore_AllProfilesStayUnderBudget()
    {
        // Act & Assert
        foreach (var (name, profile) in TestProfiles.All)
        {
            var input = BuildInputFromProfile(profile, conversationTurns: 10);
            var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

            actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
                TokenBudget,
                because: $"profile '{name}' with YAML prompt and 10 conversation turns must stay within budget");
        }
    }

    [Fact]
    public async Task AssembleAsync_WithPromptStore_TokenEstimateSumMatchesTotal()
    {
        // Arrange
        var input = BuildLeeInput(conversationTurns: 2);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var expectedTotal = _sut.EstimateTokens(actualPrompt.SystemPrompt)
            + actualPrompt.StartSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.MiddleSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.EndSections.Sum(s => s.EstimatedTokens);

        actualPrompt.EstimatedTokenCount.Should().Be(
            expectedTotal,
            because: "the reported total should match the sum of all section estimates with YAML prompt");
    }

    // ================================================================
    // Layer 1 / Layer 2 training history content tests
    // ================================================================
    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_RecentWeeksUsePerWorkoutFormat()
    {
        // Arrange — build 4 weeks of history relative to real now so the Layer 1 cutoff
        // (MaxLayer1Weeks = 2 weeks) splits them deterministically: weeks 1-2 in Layer 1,
        // weeks 3-4 in Layer 2.
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — Layer 1 per-workout detail uses pipe-separated format:
        // "YYYY-MM-DD | WorkoutType | X km | Y min | Z:ZZ/km"
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");
        var lines = historySection.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // At least some lines should match the per-workout detail pattern (pipe-separated fields)
        var perWorkoutLines = lines
            .Where(l => l.Contains(" | ") && l.Contains(" km |") && l.Contains("/km"))
            .ToList();

        perWorkoutLines.Should().NotBeEmpty(
            because: "recent workouts (within 2 weeks) should use Layer 1 per-workout detail format");

        // Verify the per-workout lines contain expected field structure
        foreach (var line in perWorkoutLines)
        {
            var fields = line.Split(" | ");
            fields.Length.Should().BeGreaterThanOrEqualTo(
                5,
                because: "per-workout format has date, type, distance, duration, and pace fields");
        }
    }

    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_OlderWeeksUseWeeklySummaryFormat()
    {
        // Arrange — 4 weeks of history; weeks 3-4 (oldest) should be in Layer 2 format
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — Layer 2 weekly summaries use "Week of YYYY-MM-DD:" prefix
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");

        historySection.Content.Should().Contain(
            "Week of",
            because: "older workouts (beyond 2 weeks) should use Layer 2 weekly summary format");

        // Weekly summary format: "Week of YYYY-MM-DD: X km total | Y runs"
        var weekSummaryLines = historySection.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("Week of", StringComparison.Ordinal))
            .ToList();

        weekSummaryLines.Should().NotBeEmpty(
            because: "there should be at least one weekly summary for older weeks");

        foreach (var line in weekSummaryLines)
        {
            line.Should().Contain(
                "km total",
                because: "weekly summaries include total distance");
            line.Should().Contain(
                "runs",
                because: "weekly summaries include run count");
        }
    }

    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_Layer1ContainsWorkoutTypesAndPaces()
    {
        // Arrange
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — per-workout detail should contain the specific workout types and pace info
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");
        var perWorkoutLines = historySection.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains(" | ") && l.Contains("/km"))
            .ToList();

        // Should contain at least one Easy and one LongRun from the recent 2 weeks
        perWorkoutLines.Should().Contain(
            l => l.Contains("Easy"),
            because: "Layer 1 should include easy runs from recent weeks");

        perWorkoutLines.Should().Contain(
            l => l.Contains("LongRun"),
            because: "Layer 1 should include long runs from recent weeks");

        // Each per-workout line should have a pace value (M:SS/km format)
        foreach (var line in perWorkoutLines)
        {
            line.Should().MatchRegex(
                @"\d+:\d{2}/km",
                because: "per-workout detail includes pace in M:SS/km format");
        }
    }

    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_Layer2IncludesLongRunDistance()
    {
        // Arrange — weekly summaries for weeks with a LongRun should include "Long run: X km"
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");
        var weekSummaryLines = historySection.Content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("Week of", StringComparison.Ordinal))
            .ToList();

        weekSummaryLines.Should().Contain(
            l => l.Contains("Long run:"),
            because: "weekly summaries for weeks with a LongRun should include the long run distance");
    }

    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_Layer1BeforeLayer2InOutput()
    {
        // Arrange
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — in the output, Layer 1 (per-workout) lines should appear before
        // Layer 2 (weekly summary) lines, because BuildTrainingHistorySection outputs
        // recent workouts first, then older weekly summaries.
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");
        var lines = historySection.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var firstPerWorkoutIndex = Array.FindIndex(lines, l => l.Contains(" | ") && l.Contains("/km"));
        var firstWeekSummaryIndex = Array.FindIndex(lines, l => l.StartsWith("Week of", StringComparison.Ordinal));

        firstPerWorkoutIndex.Should().BeGreaterThanOrEqualTo(
            0,
            because: "there should be at least one per-workout line");
        firstWeekSummaryIndex.Should().BeGreaterThanOrEqualTo(
            0,
            because: "there should be at least one weekly summary line");
        firstPerWorkoutIndex.Should().BeLessThan(
            firstWeekSummaryIndex,
            because: "Layer 1 per-workout detail should appear before Layer 2 weekly summaries");
    }

    [Fact]
    public async Task AssembleAsync_FourWeeksOfHistory_WorkoutNotesIncludedInLayer1()
    {
        // Arrange
        var input = BuildLayeredHistoryInput();

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — workouts with notes should have them appended in Layer 1 format
        var historySection = actualPrompt.MiddleSections.First(s => s.Key == "training_history");

        // The helper creates Tempo workouts with notes like "2km warm-up, 5km at tempo, 2km cool-down"
        historySection.Content.Should().Contain(
            "warm-up",
            because: "workout notes should be included in Layer 1 per-workout detail");
    }

    // ================================================================
    // Overflow cascade tests
    // ================================================================
    [Fact]
    public async Task AssembleAsync_OverflowTriggered_StaysWithinTokenBudget()
    {
        // Arrange — create input large enough to exceed the 15K token budget
        var input = BuildOverflowInput(
            workoutWeeks: 12,
            workoutsPerWeek: 6,
            conversationTurns: 20,
            charsPerMessage: 500);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — the cascade must bring the result within budget
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "the overflow cascade must enforce the 15K token budget");
    }

    [Fact]
    public async Task AssembleAsync_OverflowStep1_ReducesTrainingHistoryToLayer2Only()
    {
        // Arrange — create input where training history is large enough to exceed budget
        // but switching to Layer 2 (weekly summaries) brings it under.
        // Many workouts with verbose notes create large Layer 1 but compact Layer 2.
        var input = BuildOverflowInput(
            workoutWeeks: 8,
            workoutsPerWeek: 6,
            conversationTurns: 2,
            charsPerMessage: 100);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — Layer 2 produces weekly summaries (one line per week), not per-workout detail.
        // With Layer 2, max 4 weeks of weekly summaries should be present.
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(TokenBudget);

        var historySection = actualPrompt.MiddleSections
            .FirstOrDefault(s => s.Key == "training_history");
        historySection.Should().NotBeNull(
            because: "training history should still be present after Step 1");

        // Layer 2 format uses "Week of YYYY-MM-DD:" prefix (weekly summary format),
        // while Layer 1 uses per-workout detail "YYYY-MM-DD | WorkoutType |..."
        historySection!.Content.Should().Contain(
            "Week of",
            because: "Step 1 reduces to Layer 2 weekly summaries");
    }

    [Fact]
    public async Task AssembleAsync_OverflowStep2_TruncatesConversationHistory()
    {
        // Arrange — create input with massive conversation that stays over budget
        // even after Step 1 (Layer 2). Use many long conversation turns.
        var input = BuildOverflowInput(
            workoutWeeks: 4,
            workoutsPerWeek: 6,
            conversationTurns: 20,
            charsPerMessage: 1500);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — conversation was truncated: fewer turns than the original 20
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(TokenBudget);

        var conversationSection = actualPrompt.EndSections
            .FirstOrDefault(s => s.Key == "conversation_history");
        if (conversationSection is not null)
        {
            // Count [User]: markers to determine number of turns
            var turnCount = conversationSection.Content
                .Split("[User]:", StringSplitOptions.RemoveEmptyEntries).Length - 1;
            turnCount.Should().BeLessThan(
                20,
                because: "Step 2 truncates oldest conversation turns");
        }
    }

    [Fact]
    public async Task AssembleAsync_OverflowStep4_ReducesToRecentTwoWeeksOnly()
    {
        // Arrange — create input so large that Steps 1 and 2 are not enough.
        // Use very long conversation messages plus extensive training history.
        var input = BuildOverflowInput(
            workoutWeeks: 12,
            workoutsPerWeek: 6,
            conversationTurns: 8,
            charsPerMessage: 3000);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — training history should be reduced to at most recent 2 weeks of data
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(TokenBudget);

        var historySection = actualPrompt.MiddleSections
            .FirstOrDefault(s => s.Key == "training_history");

        // After Step 4, history may be empty (if no workouts in last 14 days)
        // or contain only data from the most recent 2 weeks.
        // Either way, it should be significantly smaller than the original 12 weeks.
        if (historySection is not null)
        {
            historySection.EstimatedTokens.Should().BeLessThan(
                1000,
                because: "Step 4 reduces training history to at most 2 recent weeks");
        }
    }

    [Fact]
    public async Task AssembleAsync_OverflowStep5_TruncatesConversationToThreeTurns()
    {
        // Arrange — create extremely large input that requires all cascade steps
        var input = BuildOverflowInput(
            workoutWeeks: 12,
            workoutsPerWeek: 6,
            conversationTurns: 20,
            charsPerMessage: 4000);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — after Step 5, conversation should have at most 3 turns
        actualPrompt.EstimatedTokenCount.Should().BeLessThanOrEqualTo(TokenBudget);

        var conversationSection = actualPrompt.EndSections
            .FirstOrDefault(s => s.Key == "conversation_history");
        if (conversationSection is not null)
        {
            var turnCount = conversationSection.Content
                .Split("[User]:", StringSplitOptions.RemoveEmptyEntries).Length - 1;
            turnCount.Should().BeLessThanOrEqualTo(
                ContextAssembler.ReducedConversationTurns,
                because: "Step 5 truncates conversation to most recent 3 turns");
        }
    }

    [Fact]
    public async Task AssembleAsync_OverflowCascade_TokenEstimateSumStillMatchesTotal()
    {
        // Arrange — use an input that triggers the overflow cascade
        var input = BuildOverflowInput(
            workoutWeeks: 10,
            workoutsPerWeek: 6,
            conversationTurns: 15,
            charsPerMessage: 1000);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — even after cascade, reported total must match sum of sections
        var expectedTotal = _sut.EstimateTokens(actualPrompt.SystemPrompt)
            + actualPrompt.StartSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.MiddleSections.Sum(s => s.EstimatedTokens)
            + actualPrompt.EndSections.Sum(s => s.EstimatedTokens);

        actualPrompt.EstimatedTokenCount.Should().Be(
            expectedTotal,
            because: "the reported total should match the sum of all section estimates after overflow cascade");
    }

    [Fact]
    public async Task AssembleAsync_OverflowCascade_CurrentUserMessageAlwaysPreserved()
    {
        // Arrange — extreme overflow that triggers all cascade steps
        var input = BuildOverflowInput(
            workoutWeeks: 12,
            workoutsPerWeek: 6,
            conversationTurns: 20,
            charsPerMessage: 4000);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — the current user message must always be present regardless of cascade
        actualPrompt.EndSections.Should().Contain(
            s => s.Key == "current_user_message",
            because: "the current user message must never be removed by the overflow cascade");
    }

    [Fact]
    public async Task AssembleAsync_OverflowCascade_StartSectionsNeverRemoved()
    {
        // Arrange — extreme overflow
        var input = BuildOverflowInput(
            workoutWeeks: 12,
            workoutsPerWeek: 6,
            conversationTurns: 20,
            charsPerMessage: 4000);

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — start sections (user profile, goal, fitness, paces) are never touched by cascade
        actualPrompt.StartSections.Should().HaveCount(
            4,
            because: "the overflow cascade never removes start sections (user profile, goal, fitness, paces)");
    }

    [Fact]
    public async Task AssembleAsync_JustUnderBudget_NoCascadeTriggered()
    {
        // Arrange — use a real profile with moderate conversation that stays under budget
        var input = BuildLeeInput(conversationTurns: 5);

        // Pre-check: verify this input is actually under budget to validate the test setup
        var preCheck = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);
        preCheck.EstimatedTokenCount.Should().BeLessThanOrEqualTo(
            TokenBudget,
            because: "this test requires an input that stays under budget without cascade");

        // Act
        var actualPrompt = await _sut.AssembleAsync(input, TestContext.Current.CancellationToken);

        // Assert — no cascade means per-workout detail should be present (Layer 1),
        // not weekly summary format. The training history should contain workout type markers.
        var historySection = actualPrompt.MiddleSections
            .FirstOrDefault(s => s.Key == "training_history");
        historySection.Should().NotBeNull();

        // Layer 1 per-workout detail contains pipe-separated fields like "Easy | 7 km"
        // Layer 2 weekly summary contains "Week of" prefix
        // When no cascade, recent workouts should be in Layer 1 format
        historySection!.Content.Should().Contain(
            " | ",
            because: "without overflow cascade, recent training history uses Layer 1 per-workout detail");
    }

    // ================================================================
    // Helper methods
    // ================================================================
    private static IPromptStore CreateMockPromptStore()
    {
        var store = Substitute.For<IPromptStore>();

        var template = new PromptTemplate(
            Id: "coaching-system",
            Version: "v1",
            StaticSystemPrompt: BuildYamlSystemPrompt(),
            ContextTemplate: BuildYamlContextTemplate(),
            Metadata: new PromptMetadata("Test prompt", "Test", "2026-01-01"));

        store.GetActiveVersion("coaching-system").Returns("v1");
        store.GetPromptAsync("coaching-system", "v1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(template));

        return store;
    }

    private static string BuildYamlSystemPrompt()
    {
        return """
            You are an experienced, evidence-based running coach. You combine deep knowledge of exercise physiology with genuine care for the runner as a whole person. You are warm, direct, and knowledgeable.

            COMMUNICATION FRAMEWORK:
            Layer 1 - OARS (moment-to-moment): Every response contains at least one Open question, Affirmation, Reflection, or Summary.
            Layer 2 - Elicit-Provide-Elicit (information delivery): When sharing training knowledge, always ask first, share, then check.
            Layer 3 - Modified GROW (conversation structure): Goal, Reality, Options, Way Forward.

            SAFETY RULES:
            Medical Boundary: You are a running coach, not a medical professional.
            Injury Protocol: When a runner reports an injury, recommend consulting a medical professional.
            Crisis Response: If crisis language is used, STOP coaching, provide crisis resources (988, 741741).
            Nutrition Boundary: General fueling timing only. Recommend a registered dietitian for specifics.
            Overtraining Detection: Acknowledge, suggest reducing load, do not push through.

            DETERMINISTIC GUARDRAILS:
            The training paces provided are computed deterministically from the runner's race history using Daniels' Running Formula.
            You MUST use these exact pace ranges. Volume progression MUST NOT exceed 10% per week.

            SEMANTIC OUTPUT GUIDANCE:
            When generating a training plan, explain your reasoning and include physiological rationale.
            """;
    }

    private static string BuildYamlContextTemplate()
    {
        return """
            === RUNNER PROFILE ===
            {{profile}}

            === CURRENT GOAL ===
            {{goal}}

            === FITNESS ASSESSMENT ===
            {{fitness}}

            === TRAINING PACES (computed from VDOT — use these exactly) ===
            {{training_paces}}

            === RECENT TRAINING HISTORY ===
            {{training_history}}

            === CONVERSATION HISTORY ===
            {{conversation}}
            """;
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

    /// <summary>
    /// Builds a <see cref="ContextAssemblerInput"/> with 4 weeks of training history
    /// relative to actual DateTime.UtcNow, ensuring a deterministic Layer 1/Layer 2 split.
    /// Weeks 1-2 (most recent) fall within the MaxLayer1Weeks cutoff (per-workout detail),
    /// weeks 3-4 fall outside the cutoff (weekly summaries).
    /// </summary>
    private static ContextAssemblerInput BuildLayeredHistoryInput()
    {
        var lee = TestProfiles.Lee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var workouts = ImmutableArray.CreateBuilder<WorkoutSummary>();
        var easyPace = TimeSpan.FromMinutes(5.5);
        var tempoPace = TimeSpan.FromMinutes(4.8);

        for (var week = 4; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);

            // Monday: easy run 7km
            workouts.Add(new WorkoutSummary(
                weekStart,
                "Easy",
                7m,
                (int)(7m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Wednesday: tempo run 8km
            workouts.Add(new WorkoutSummary(
                weekStart.AddDays(2),
                "Tempo",
                8m,
                (int)(8m * (decimal)tempoPace.TotalMinutes),
                tempoPace,
                "2km warm-up, 5km at tempo, 2km cool-down"));

            // Friday: easy run 6km
            workouts.Add(new WorkoutSummary(
                weekStart.AddDays(4),
                "Easy",
                6m,
                (int)(6m * (decimal)easyPace.TotalMinutes),
                easyPace,
                null));

            // Sunday: long run (12-15km progressive)
            var longRunKm = 12m + (4 - week);
            workouts.Add(new WorkoutSummary(
                weekStart.AddDays(6),
                "LongRun",
                longRunKm,
                (int)(longRunKm * (decimal)easyPace.TotalMinutes * 1.1m),
                easyPace,
                null));
        }

        return new ContextAssemblerInput(
            lee.UserProfile,
            lee.GoalState,
            lee.GoalState.CurrentFitnessEstimate,
            lee.GoalState.CurrentFitnessEstimate.TrainingPaces,
            workouts.ToImmutable(),
            ImmutableArray<ConversationTurn>.Empty,
            "Create a training plan for my half marathon.");
    }

    /// <summary>
    /// Builds a <see cref="ContextAssemblerInput"/> with enough content to exceed
    /// the 15K token budget, triggering the overflow cascade.
    /// </summary>
    /// <param name="workoutWeeks">Number of weeks of training history to generate.</param>
    /// <param name="workoutsPerWeek">Workouts per week in training history.</param>
    /// <param name="conversationTurns">Number of conversation turns.</param>
    /// <param name="charsPerMessage">Characters per user/coach message in conversation.</param>
    private static ContextAssemblerInput BuildOverflowInput(
        int workoutWeeks,
        int workoutsPerWeek,
        int conversationTurns,
        int charsPerMessage)
    {
        var lee = TestProfiles.Lee();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Build large training history.
        var workouts = ImmutableArray.CreateBuilder<WorkoutSummary>();
        for (var week = workoutWeeks; week >= 1; week--)
        {
            var weekStart = today.AddDays(-7 * week);
            for (var day = 0; day < workoutsPerWeek; day++)
            {
                var workoutDate = weekStart.AddDays(day);
                var workoutType = day == 0 ? "LongRun" : "Easy";
                var notes = $"Workout notes for week {week}, day {day + 1}. " +
                    $"Felt good during the run, maintained target pace throughout. " +
                    $"Weather was mild, slight headwind on the return.";
                workouts.Add(new WorkoutSummary(
                    workoutDate,
                    workoutType,
                    8m + day,
                    45 + (day * 5),
                    TimeSpan.FromMinutes(5.5),
                    notes));
            }
        }

        // Build long conversation history.
        var conversation = ImmutableArray.CreateBuilder<ConversationTurn>();
        for (var i = 1; i <= conversationTurns; i++)
        {
            var padding = new string('x', Math.Max(0, charsPerMessage - 60));
            conversation.Add(new ConversationTurn(
                $"User message {i}: How should I approach my training? {padding}",
                $"Coach response {i}: Focus on easy runs with quality. {padding}"));
        }

        return new ContextAssemblerInput(
            lee.UserProfile,
            lee.GoalState,
            lee.GoalState.CurrentFitnessEstimate,
            lee.GoalState.CurrentFitnessEstimate.TrainingPaces,
            workouts.ToImmutable(),
            conversation.ToImmutable(),
            "Create a training plan for my half marathon.");
    }
}
