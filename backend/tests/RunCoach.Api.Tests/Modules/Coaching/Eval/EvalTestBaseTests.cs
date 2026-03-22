using FluentAssertions;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for <see cref="EvalTestBase"/> static helpers and infrastructure.
/// These tests do NOT require an API key since they only test non-LLM functionality.
/// </summary>
public class EvalTestBaseTests
{
    [Fact]
    public void LoadProfile_ValidName_ReturnsProfile()
    {
        // Arrange & Act
        var profile = EvalTestBase.LoadProfile("lee");

        // Assert
        profile.Should().NotBeNull();
        profile.UserProfile.Name.Should().Be("Lee");
    }

    [Fact]
    public void LoadProfile_CaseInsensitive_ReturnsProfile()
    {
        // Arrange & Act
        var profile = EvalTestBase.LoadProfile("Sarah");

        // Assert
        profile.Should().NotBeNull();
        profile.UserProfile.Name.Should().Be("Sarah");
    }

    [Fact]
    public void LoadProfile_InvalidName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => EvalTestBase.LoadProfile("unknown");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name")
            .WithMessage("*Unknown profile*");
    }

    [Fact]
    public void LoadProfile_AllFiveProfiles_LoadSuccessfully()
    {
        // Arrange
        var expectedNames = new[] { "sarah", "lee", "maria", "james", "priya" };

        // Act & Assert
        foreach (var name in expectedNames)
        {
            var profile = EvalTestBase.LoadProfile(name);
            profile.Should().NotBeNull(because: $"profile '{name}' should be loadable");
            profile.UserProfile.Should().NotBeNull();
            profile.GoalState.Should().NotBeNull();
        }
    }

    [Fact]
    public void GetOutputDirectory_ReturnsPathContainingPoc1EvalResults()
    {
        // Arrange & Act
        var actualDir = EvalTestBase.GetOutputDirectory();

        // Assert
        actualDir.Should().EndWith("poc1-eval-results");
        actualDir.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void WriteEvalResult_WritesJsonFile()
    {
        // Arrange
        var scenarioName = $"test-write-{Guid.NewGuid():N}";
        var testData = new { Message = "test eval output", Timestamp = DateTime.UtcNow.ToString("o") };

        try
        {
            // Act
            EvalTestBase.WriteEvalResult(scenarioName, testData);

            // Assert
            var outputPath = Path.Combine(EvalTestBase.GetOutputDirectory(), $"{scenarioName}.json");
            File.Exists(outputPath).Should().BeTrue();

            var content = File.ReadAllText(outputPath);
            content.Should().Contain("test eval output");
        }
        finally
        {
            // Cleanup
            var outputPath = Path.Combine(EvalTestBase.GetOutputDirectory(), $"{scenarioName}.json");
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
