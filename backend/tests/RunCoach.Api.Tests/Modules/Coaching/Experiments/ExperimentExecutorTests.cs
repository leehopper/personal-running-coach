using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Experiments;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Tests for <see cref="ExperimentExecutor"/> verifying that all 4 experiments
/// run correctly across baseline (Lee) and cross-validation (Maria) profiles
/// in dry-run mode. Validates result structure, token budgets, section placement,
/// summarization differences, and conversation history impact.
/// </summary>
public class ExperimentExecutorTests : IDisposable
{
    private readonly string _testOutputDir;
    private readonly ExperimentExecutor _sut;

    public ExperimentExecutorTests()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), "runcoach-executor-tests", Guid.NewGuid().ToString());
        _sut = new ExperimentExecutor(_testOutputDir);
    }

    [Fact]
    public async Task RunAllExperiments_DryRun_Returns22Results()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — 3 token + 3 positional + 3 summarization + 2 conversation = 11 variations x 2 profiles = 22
        actual.TotalRuns.Should().Be(22);
        actual.PassedRuns.Should().Be(22);
        actual.FailedRuns.Should().Be(0);
        actual.IsLiveRun.Should().BeFalse();
    }

    [Fact]
    public async Task RunAllExperiments_DryRun_AllResultsHaveNoLlmResponse()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Results.Should().OnlyContain(
            r => r.LlmResponse == null,
            because: "dry runs do not call the LLM");
    }

    [Fact]
    public async Task RunAllExperiments_DryRun_AllResultsHaveNoErrors()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Results.Should().OnlyContain(
            r => r.Error == null,
            because: "dry runs should not produce errors");
    }

    [Fact]
    public async Task RunAllExperiments_DryRun_UsesBothProfiles()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var profiles = actual.Results.Select(r => r.ProfileName).Distinct().OrderBy(p => p).ToList();
        profiles.Should().Contain("lee", because: "Lee is the baseline profile");
        profiles.Should().Contain("maria", because: "Maria is the cross-validation profile");
    }

    [Fact]
    public async Task RunAllExperiments_DryRun_HasTimestamp()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Timestamp.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TokenBudget_DryRun_Returns6Results()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — 3 variations x 2 profiles = 6 runs
        actual.TokenBudgetResults.Should().HaveCount(6);
    }

    [Fact]
    public async Task TokenBudget_DryRun_AllVariationsRespectBudget()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        foreach (var result in actual.TokenBudgetResults)
        {
            var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == result.VariationId);
            result.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(
                config.TotalTokenBudget,
                because: $"{result.VariationId} for {result.ProfileName} must stay within {config.TotalTokenBudget} budget");
        }
    }

    [Fact]
    public async Task TokenBudget_DryRun_8KUsesFewerTokensThan15K()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var lee8k = actual.TokenBudgetResults.First(r => r.VariationId == "token-8k" && r.ProfileName == "lee");
        var lee15k = actual.TokenBudgetResults.First(r => r.VariationId == "token-15k" && r.ProfileName == "lee");
        lee8k.EstimatedPromptTokens.Should().BeLessThan(lee15k.EstimatedPromptTokens);
    }

    [Fact]
    public async Task TokenBudget_DryRun_12KIsBetween8KAnd15K()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var lee8k = actual.TokenBudgetResults.First(r => r.VariationId == "token-8k" && r.ProfileName == "lee");
        var lee12k = actual.TokenBudgetResults.First(r => r.VariationId == "token-12k" && r.ProfileName == "lee");
        var lee15k = actual.TokenBudgetResults.First(r => r.VariationId == "token-15k" && r.ProfileName == "lee");

        lee12k.EstimatedPromptTokens.Should().BeGreaterThanOrEqualTo(lee8k.EstimatedPromptTokens);
        lee12k.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(lee15k.EstimatedPromptTokens);
    }

    [Fact]
    public async Task TokenBudget_CrossValidation_MariaAlsoRespectsBudgets()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var mariaResults = actual.TokenBudgetResults.Where(r => r.ProfileName == "maria").ToList();
        mariaResults.Should().HaveCount(3);

        foreach (var result in mariaResults)
        {
            var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == result.VariationId);
            result.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(config.TotalTokenBudget);
        }
    }

    [Fact]
    public async Task PositionalPlacement_DryRun_Returns6Results()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — 3 variations x 2 profiles = 6 runs
        actual.PositionalPlacementResults.Should().HaveCount(6);
    }

    [Fact]
    public async Task PositionalPlacement_StartVariation_HasProfileInStartSections()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var startResults = actual.PositionalPlacementResults
            .Where(r => r.VariationId == "position-start")
            .ToList();
        startResults.Should().OnlyContain(
            r => r.StartSectionCount >= 3,
            because: "profile-at-start should have user_profile, goal_state, fitness_estimate in start");
    }

    [Fact]
    public async Task PositionalPlacement_MiddleVariation_HasProfileInMiddleSections()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var middleResults = actual.PositionalPlacementResults
            .Where(r => r.VariationId == "position-middle")
            .ToList();
        middleResults.Should().OnlyContain(
            r => r.MiddleSectionCount >= 3,
            because: "profile-in-middle should have profile sections in middle");
    }

    [Fact]
    public async Task PositionalPlacement_EndVariation_HasProfileInEndSections()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var endResults = actual.PositionalPlacementResults
            .Where(r => r.VariationId == "position-end")
            .ToList();
        endResults.Should().OnlyContain(
            r => r.EndSectionCount >= 3,
            because: "profile-at-end should have profile sections in end");
    }

    [Fact]
    public async Task PositionalPlacement_AllVariations_HaveAtLeastOneStartSection()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — training_paces should always remain in start regardless of placement
        actual.PositionalPlacementResults.Should().OnlyContain(
            r => r.StartSectionCount >= 1,
            because: "training paces should always remain in start sections");
    }

    [Fact]
    public async Task SummarizationLevel_DryRun_Returns6Results()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — 3 variations x 2 profiles = 6 runs
        actual.SummarizationLevelResults.Should().HaveCount(6);
    }

    [Fact]
    public async Task SummarizationLevel_WeeklyUsesFewerTokensThanPerWorkout()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var leeWeekly = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-weekly" && r.ProfileName == "lee");
        var leePerWorkout = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-per-workout" && r.ProfileName == "lee");

        leeWeekly.EstimatedPromptTokens.Should().BeLessThan(leePerWorkout.EstimatedPromptTokens);
    }

    [Fact]
    public async Task SummarizationLevel_MixedDoesNotExceedPerWorkout()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var leeMixed = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-mixed" && r.ProfileName == "lee");
        var leePerWorkout = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-per-workout" && r.ProfileName == "lee");

        leeMixed.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(leePerWorkout.EstimatedPromptTokens);
    }

    [Fact]
    public async Task SummarizationLevel_CrossValidation_MariaSamePattern()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var mariaWeekly = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-weekly" && r.ProfileName == "maria");
        var mariaPerWorkout = actual.SummarizationLevelResults
            .First(r => r.VariationId == "summarize-per-workout" && r.ProfileName == "maria");

        mariaWeekly.EstimatedPromptTokens.Should().BeLessThan(mariaPerWorkout.EstimatedPromptTokens);
    }

    [Fact]
    public async Task ConversationHistory_DryRun_Returns4Results()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert — 2 variations x 2 profiles = 4 runs
        actual.ConversationHistoryResults.Should().HaveCount(4);
    }

    [Fact]
    public async Task ConversationHistory_5TurnsUsesMoreTokensThan0Turns()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var lee0 = actual.ConversationHistoryResults
            .First(r => r.VariationId == "conversation-0" && r.ProfileName == "lee");
        var lee5 = actual.ConversationHistoryResults
            .First(r => r.VariationId == "conversation-5" && r.ProfileName == "lee");

        lee5.EstimatedPromptTokens.Should().BeGreaterThan(lee0.EstimatedPromptTokens);
    }

    [Fact]
    public async Task ConversationHistory_CrossValidation_MariaSamePattern()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        var maria0 = actual.ConversationHistoryResults
            .First(r => r.VariationId == "conversation-0" && r.ProfileName == "maria");
        var maria5 = actual.ConversationHistoryResults
            .First(r => r.VariationId == "conversation-5" && r.ProfileName == "maria");

        maria5.EstimatedPromptTokens.Should().BeGreaterThan(maria0.EstimatedPromptTokens);
    }

    [Fact]
    public async Task Analyze_DryRun_ProducesValidAnalysis()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        var actual = ExperimentExecutor.Analyze(suite);

        // Assert
        actual.TotalExperimentRuns.Should().Be(22);
        actual.TotalLiveResponses.Should().Be(0);
        actual.TotalDryRuns.Should().Be(22);
        actual.AllProfilesUsed.Should().Contain("lee");
        actual.AllProfilesUsed.Should().Contain("maria");
    }

    [Fact]
    public async Task Analyze_TokenBudget_HasTokenAverages()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        var actual = ExperimentExecutor.Analyze(suite);

        // Assert
        actual.TokenBudgetObservations.AverageTokensByVariation.Should().ContainKey("token-8k");
        actual.TokenBudgetObservations.AverageTokensByVariation.Should().ContainKey("token-12k");
        actual.TokenBudgetObservations.AverageTokensByVariation.Should().ContainKey("token-15k");
        actual.TokenBudgetObservations.MinTokensUsed.Should().BeGreaterThan(0);
        actual.TokenBudgetObservations.MaxTokensUsed.Should().BeGreaterThan(
            actual.TokenBudgetObservations.MinTokensUsed);
    }

    [Fact]
    public async Task Analyze_SummarizationLevel_WeeklyUsesFewerTokens()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        var actual = ExperimentExecutor.Analyze(suite);

        // Assert
        actual.SummarizationLevelObservations.WeeklySummaryUsesFewerTokens.Should().BeTrue();
        actual.SummarizationLevelObservations.TokenSavingsPercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_ConversationHistory_AddsTokens()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        var actual = ExperimentExecutor.Analyze(suite);

        // Assert
        actual.ConversationHistoryObservations.ConversationAddsTokens.Should().BeTrue();
        actual.ConversationHistoryObservations.AdditionalTokensFromConversation.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Analyze_CrossValidation_BothProfilesHaveResults()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        var actual = ExperimentExecutor.Analyze(suite);

        // Assert
        actual.CrossValidationObservations.BothProfilesHaveResults.Should().BeTrue();
        actual.CrossValidationObservations.BaselineProfile.Should().Be("lee");
        actual.CrossValidationObservations.CrossValidationProfile.Should().Be("maria");
        actual.CrossValidationObservations.BaselineRunCount.Should().Be(11);
        actual.CrossValidationObservations.CrossValidationRunCount.Should().Be(11);
    }

    [Fact]
    public async Task WriteResults_CreatesAllOutputFiles()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        _sut.WriteResults(suite);

        // Assert
        File.Exists(Path.Combine(_testOutputDir, "00-experiment-suite-summary.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "01-token-budget-results.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "02-positional-placement-results.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "03-summarization-level-results.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "04-conversation-history-results.json")).Should().BeTrue();
    }

    [Fact]
    public async Task WriteResults_CreatesIndividualResultFiles()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);

        // Act
        _sut.WriteResults(suite);

        // Assert
        File.Exists(Path.Combine(_testOutputDir, "token-8k-lee.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "token-15k-maria.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "position-start-lee.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "summarize-weekly-maria.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "conversation-0-lee.json")).Should().BeTrue();
        File.Exists(Path.Combine(_testOutputDir, "conversation-5-maria.json")).Should().BeTrue();
    }

    [Fact]
    public async Task WriteResults_SummaryFileContainsExpectedData()
    {
        // Arrange
        var suite = await _sut.RunAllExperimentsAsync(live: false);
        _sut.WriteResults(suite);

        // Act
        var summaryContent = await File.ReadAllTextAsync(
            Path.Combine(_testOutputDir, "00-experiment-suite-summary.json"));

        // Assert
        summaryContent.Should().Contain("totalRuns");
        summaryContent.Should().Contain("22");
        summaryContent.Should().Contain("lee");
        summaryContent.Should().Contain("maria");
    }

    [Fact]
    public async Task AllResults_HaveTimestamps()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Timestamp));
    }

    [Fact]
    public async Task AllResults_HavePositiveSectionCounts()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Results.Should().OnlyContain(r => r.SectionCount > 0);
    }

    [Fact]
    public async Task AllResults_SectionCountsAreConsistent()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        foreach (var result in actual.Results)
        {
            var expectedSectionCount = result.StartSectionCount + result.MiddleSectionCount + result.EndSectionCount;
            result.SectionCount.Should().Be(
                expectedSectionCount,
                because: $"section counts for {result.VariationId}/{result.ProfileName} must sum correctly");
        }
    }

    [Fact]
    public async Task AllResults_HavePositiveTokenCounts()
    {
        // Act
        var actual = await _sut.RunAllExperimentsAsync(live: false);

        // Assert
        actual.Results.Should().OnlyContain(r => r.EstimatedPromptTokens > 0);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && Directory.Exists(_testOutputDir))
        {
            try
            {
                Directory.Delete(_testOutputDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
