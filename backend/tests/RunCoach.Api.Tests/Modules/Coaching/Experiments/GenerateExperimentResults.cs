using System.Text.Json;
using System.Text.Json.Serialization;
using RunCoach.Api.Modules.Coaching.Experiments;

namespace RunCoach.Api.Tests.Modules.Coaching.Experiments;

/// <summary>
/// Generates experiment dry-run results to the poc1-eval-results/experiments directory.
/// This is a one-off test for generating the committed result artifacts.
/// Tagged with Trait("Category", "ResultGeneration") so it can be run explicitly.
/// </summary>
public class GenerateExperimentResults
{
    private static readonly JsonSerializerOptions AnalysisWriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    [Trait("Category", "ResultGeneration")]
    public async Task GenerateAllDryRunResults()
    {
        // Arrange — find the repo root by walking up from the test assembly location.
        var testDir = AppContext.BaseDirectory;
        var repoRoot = FindRepoRoot(testDir);
        var outputDir = Path.Combine(repoRoot, "backend", "poc1-eval-results", "experiments");
        Directory.CreateDirectory(outputDir);

        var executor = new ExperimentExecutor(outputDir);

        // Act — run all experiments in dry-run mode.
        var suite = await executor.RunAllExperimentsAsync(live: false);
        executor.WriteResults(suite);

        // Also write analysis results.
        var analysis = ExperimentExecutor.Analyze(suite);
        var analysisJson = JsonSerializer.Serialize(analysis, AnalysisWriteOptions);
        await File.WriteAllTextAsync(
            Path.Combine(outputDir, "05-analysis-summary.json"),
            analysisJson);

        // Assert — verify files were created.
        Assert.True(File.Exists(Path.Combine(outputDir, "00-experiment-suite-summary.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "05-analysis-summary.json")));
        Assert.Equal(22, suite.TotalRuns);
    }

    private static string FindRepoRoot(string startDir)
    {
        var dir = startDir;
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root from " + startDir);
    }
}
