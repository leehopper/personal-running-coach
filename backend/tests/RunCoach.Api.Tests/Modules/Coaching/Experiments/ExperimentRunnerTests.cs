using System.Collections.Immutable;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Experiments;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Tests for <see cref="ExperimentRunner"/> verifying dry-run mode,
/// result writing, and experiment execution infrastructure.
/// </summary>
public class ExperimentRunnerTests : IDisposable
{
    private readonly string _testOutputDir;
    private readonly ExperimentRunner _sut;

    public ExperimentRunnerTests()
    {
        _testOutputDir = Path.Combine(Path.GetTempPath(), "runcoach-experiment-tests", Guid.NewGuid().ToString());
        _sut = new ExperimentRunner(_testOutputDir);
    }

    [Fact]
    public void DryRun_TokenBudget8K_ReturnsResult()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-8k");

        // Act
        var actual = _sut.DryRun(config, "lee");

        // Assert
        actual.VariationId.Should().Be("token-8k");
        actual.ProfileName.Should().Be("lee");
        actual.Category.Should().Be(ExperimentCategory.TokenBudget);
        actual.EstimatedPromptTokens.Should().BeGreaterThan(0);
        actual.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(8_000);
        actual.SectionCount.Should().BeGreaterThan(0);
        actual.LlmResponse.Should().BeNull(because: "dry runs do not call the LLM");
        actual.Error.Should().BeNull();
    }

    [Fact]
    public void DryRun_AllTokenBudgetVariations_StayWithinBudget()
    {
        // Act & Assert
        foreach (var config in ExperimentVariations.TokenBudget)
        {
            var actual = _sut.DryRun(config, "lee");
            actual.EstimatedPromptTokens.Should().BeLessThanOrEqualTo(
                config.TotalTokenBudget,
                because: $"variation '{config.VariationId}' must stay within {config.TotalTokenBudget} budget");
        }
    }

    [Fact]
    public void DryRun_PositionalPlacement_ReturnsValidResults()
    {
        // Act & Assert
        foreach (var config in ExperimentVariations.PositionalPlacement)
        {
            var actual = _sut.DryRun(config, "lee");
            actual.VariationId.Should().Be(config.VariationId);
            actual.SectionCount.Should().BeGreaterThan(0);
            actual.StartSectionCount.Should().BeGreaterThan(
                0,
                because: "at least the training paces should remain in start");
        }
    }

    [Fact]
    public void DryRun_InvalidProfile_ThrowsArgumentException()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget[0];

        // Act
        var act = () => _sut.DryRun(config, "nonexistent");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown profile*");
    }

    [Fact]
    public void DryRun_CustomUserMessage_AcceptsMessage()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget[0];

        // Act
        var actual = _sut.DryRun(config, "lee", "Custom message for testing.");

        // Assert
        actual.Should().NotBeNull();
        actual.EstimatedPromptTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DryRun_MariaProfile_WorksWithAllVariations()
    {
        // Act & Assert
        foreach (var config in ExperimentVariations.All)
        {
            var actual = _sut.DryRun(config, "maria");
            actual.ProfileName.Should().Be("maria");
            actual.EstimatedPromptTokens.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task RunAllAsync_DryRun_ReturnsAllResults()
    {
        // Act
        var actual = await _sut.RunAllAsync(
            ExperimentVariations.TokenBudget,
            "lee",
            live: false,
            ct: TestContext.Current.CancellationToken);

        // Assert
        actual.Should().HaveCount(ExperimentVariations.TokenBudget.Length);
        actual.Should().OnlyContain(r => r.LlmResponse == null);
    }

    [Fact]
    public async Task LiveRunAsync_NoLlmConfigured_ThrowsInvalidOperation()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget[0];

        // Act
        var act = async () => await _sut.LiveRunAsync(config, "lee", ct: TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*LLM client is not configured*");
    }

    [Fact]
    public void WriteResults_CreatesOutputFile()
    {
        // Arrange
        var results = ImmutableArray.Create(
            _sut.DryRun(ExperimentVariations.TokenBudget[0], "lee"));

        // Act
        _sut.WriteResults("test-results.json", results);

        // Assert
        var expectedPath = Path.Combine(_testOutputDir, "test-results.json");
        File.Exists(expectedPath).Should().BeTrue();
        var content = File.ReadAllText(expectedPath);
        content.Should().Contain("token-8k");
        content.Should().Contain("lee");
    }

    [Fact]
    public void WriteResult_CreatesIndividualFile()
    {
        // Arrange
        var result = _sut.DryRun(ExperimentVariations.TokenBudget[0], "lee");

        // Act
        _sut.WriteResult(result);

        // Assert
        var expectedPath = Path.Combine(_testOutputDir, "token-8k-lee.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public void DryRun_ResultHasTimestamp()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget[0];

        // Act
        var actual = _sut.DryRun(config, "lee");

        // Assert
        actual.Timestamp.Should().NotBeNullOrWhiteSpace();
        DateTime.TryParse(actual.Timestamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _).Should().BeTrue();
    }

    [Fact]
    public void DryRun_SectionCounts_AreConsistent()
    {
        // Arrange
        var config = ExperimentVariations.TokenBudget.First(v => v.VariationId == "token-15k");

        // Act
        var actual = _sut.DryRun(config, "lee");

        // Assert
        actual.SectionCount.Should().Be(
            actual.StartSectionCount + actual.MiddleSectionCount + actual.EndSectionCount,
            because: "total section count should equal sum of individual section counts");
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
