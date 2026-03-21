using System.Text.Json;
using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Unit tests for the static helper methods on <see cref="EvalTestBase"/>.
/// These tests do NOT require an API key since they only test parsing/extraction logic.
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
    public void ExtractJsonBlock_WithValidJsonBlock_ReturnsJson()
    {
        // Arrange
        var response = """
            Here is your training plan:

            ```json
            {
              "macroPlan": {
                "phases": ["base", "build"]
              }
            }
            ```

            These phases will help you build fitness gradually.
            """;

        // Act
        var actualJson = EvalTestBase.ExtractJsonBlock(response);

        // Assert
        actualJson.Should().NotBeNull();
        actualJson.Should().Contain("macroPlan");
        actualJson.Should().Contain("phases");
    }

    [Fact]
    public void ExtractJsonBlock_NoJsonBlock_ReturnsNull()
    {
        // Arrange
        var response = "Here is your training plan in plain text format.";

        // Act
        var actualJson = EvalTestBase.ExtractJsonBlock(response);

        // Assert
        actualJson.Should().BeNull();
    }

    [Fact]
    public void ExtractJsonBlock_UnclosedJsonBlock_ReturnsNull()
    {
        // Arrange
        var response = """
            ```json
            { "incomplete": true
            """;

        // Act
        var actualJson = EvalTestBase.ExtractJsonBlock(response);

        // Assert
        actualJson.Should().BeNull();
    }

    [Fact]
    public void ParsePlanJson_ValidJsonBlock_ReturnsJsonElement()
    {
        // Arrange
        var response = """
            ```json
            {
              "macroPlan": { "phases": ["base"] },
              "mesoWeek": { "weekNumber": 1 }
            }
            ```
            """;

        // Act
        var actualJson = EvalTestBase.ParsePlanJson(response);

        // Assert
        actualJson.Should().NotBeNull();
        actualJson!.Value.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void ParsePlanJson_InvalidJson_ReturnsNull()
    {
        // Arrange
        var response = """
            ```json
            { not valid json at all
            ```
            """;

        // Act
        var actualJson = EvalTestBase.ParsePlanJson(response);

        // Assert
        actualJson.Should().BeNull();
    }

    [Fact]
    public void ParsePlanJson_NoJsonBlock_ReturnsNull()
    {
        // Arrange
        var response = "No JSON here.";

        // Act
        var actualJson = EvalTestBase.ParsePlanJson(response);

        // Assert
        actualJson.Should().BeNull();
    }

    [Fact]
    public void ExtractMacroPlan_WithMacroPlanKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "macroPlan": { "phases": ["base", "build", "peak"] }
            }
            """).RootElement;

        // Act
        var actualPlan = EvalTestBase.ExtractMacroPlan(json);

        // Assert
        actualPlan.Should().NotBeNull();
        actualPlan!.Value.GetProperty("phases").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ExtractMacroPlan_WithSnakeCaseKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "macro_plan": { "phases": ["base"] }
            }
            """).RootElement;

        // Act
        var actualPlan = EvalTestBase.ExtractMacroPlan(json);

        // Assert
        actualPlan.Should().NotBeNull();
    }

    [Fact]
    public void ExtractMacroPlan_MissingKey_ReturnsNull()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "otherData": { "value": 1 }
            }
            """).RootElement;

        // Act
        var actualPlan = EvalTestBase.ExtractMacroPlan(json);

        // Assert
        actualPlan.Should().BeNull();
    }

    [Fact]
    public void ExtractMesoWeek_WithMesoWeekKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "mesoWeek": { "weekNumber": 1 }
            }
            """).RootElement;

        // Act
        var actualWeek = EvalTestBase.ExtractMesoWeek(json);

        // Assert
        actualWeek.Should().NotBeNull();
        actualWeek!.Value.GetProperty("weekNumber").GetInt32().Should().Be(1);
    }

    [Fact]
    public void ExtractMesoWeek_WithWeekTemplateKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "weekTemplate": { "weekNumber": 2 }
            }
            """).RootElement;

        // Act
        var actualWeek = EvalTestBase.ExtractMesoWeek(json);

        // Assert
        actualWeek.Should().NotBeNull();
    }

    [Fact]
    public void ExtractMicroWorkouts_WithWorkoutsKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "microWorkouts": [
                { "date": "2026-03-22", "type": "easy" },
                { "date": "2026-03-24", "type": "tempo" }
              ]
            }
            """).RootElement;

        // Act
        var actualWorkouts = EvalTestBase.ExtractMicroWorkouts(json);

        // Assert
        actualWorkouts.Should().NotBeNull();
        actualWorkouts!.Value.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void ExtractMicroWorkouts_WithSnakeCaseKey_ReturnsElement()
    {
        // Arrange
        var json = JsonDocument.Parse("""
            {
              "micro_workouts": [{ "date": "2026-03-22" }]
            }
            """).RootElement;

        // Act
        var actualWorkouts = EvalTestBase.ExtractMicroWorkouts(json);

        // Assert
        actualWorkouts.Should().NotBeNull();
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

    [Fact]
    public void WriteEvalResult_WithMetadata_WritesCompleteFile()
    {
        // Arrange
        var scenarioName = $"test-metadata-{Guid.NewGuid():N}";

        try
        {
            // Act
            EvalTestBase.WriteEvalResult(scenarioName, "lee", "Sample LLM response text", 5000);

            // Assert
            var outputPath = Path.Combine(EvalTestBase.GetOutputDirectory(), $"{scenarioName}.json");
            File.Exists(outputPath).Should().BeTrue();

            var content = File.ReadAllText(outputPath);
            content.Should().Contain("lee");
            content.Should().Contain("Sample LLM response text");
            content.Should().Contain("5000");
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
