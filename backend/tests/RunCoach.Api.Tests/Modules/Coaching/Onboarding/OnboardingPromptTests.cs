using FluentAssertions;

namespace RunCoach.Api.Tests.Modules.Coaching.Onboarding;

/// <summary>
/// Trademark + structural assertions on the on-disk onboarding system prompt YAML.
/// Per DEC-042 the user-facing surface must use "Daniels-Gilbert zones" or "pace-zone index" -
/// never the trademarked term VDOT. The R-068 / DEC-059 data_handling directive must sit at
/// the END of the static system prompt block so it lands inside the Anthropic prefix cache.
/// </summary>
public sealed class OnboardingPromptTests
{
    // Resolved per-test rather than via a static initializer so a missing or moved YAML
    // surfaces as a single test failure (with the resolution error in the assertion
    // message) instead of a TypeInitializationException that fails every fact in this
    // class before any assertion runs.
    private static string PromptFilePath => ResolvePromptFile();

    [Fact]
    public void OnboardingV1Yaml_FileExists()
    {
        // Arrange + Act
        var actual = File.Exists(PromptFilePath);

        // Assert
        actual.Should().BeTrue($"the onboarding-v1 prompt file must exist at '{PromptFilePath}'");
    }

    [Fact]
    public void OnboardingV1Yaml_ContainsNoVdotTrademark()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var containsVdot = promptText.Contains("vdot", StringComparison.OrdinalIgnoreCase);

        // Assert
        containsVdot.Should().BeFalse(
            "the assembled onboarding system prompt must not reference the trademarked term VDOT (DEC-042 / NOTICE)");
    }

    [Fact]
    public void OnboardingV1Yaml_UsesDanielsGilbertVocabulary()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var hasGenericVocab = promptText.Contains("Daniels-Gilbert zones", StringComparison.Ordinal)
            || promptText.Contains("pace-zone index", StringComparison.Ordinal);

        // Assert
        hasGenericVocab.Should().BeTrue(
            "the onboarding prompt must use 'Daniels-Gilbert zones' or 'pace-zone index' to refer to pace methodology");
    }

    [Fact]
    public void OnboardingV1Yaml_ContainsDataHandlingDirective()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var hasDirective = promptText.Contains("data_handling", StringComparison.Ordinal)
            && promptText.Contains("SECTION_NAME", StringComparison.Ordinal);

        // Assert
        hasDirective.Should().BeTrue(
            "the onboarding prompt must carry the R-068 / DEC-059 data_handling directive referencing SECTION_NAME delimiters");
    }

    [Fact]
    public void OnboardingV1Yaml_DataHandlingDirectiveSitsAtEndOfSystemBlock()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);
        var dataHandlingIndex = promptText.IndexOf("data_handling", StringComparison.Ordinal);
        var contextTemplateIndex = promptText.IndexOf("context_template:", StringComparison.Ordinal);

        // Act + Assert — directive must appear before the context_template block boundary so it
        // lands inside the cacheable system-prompt prefix per R-068 §8 + DEC-059.
        dataHandlingIndex.Should().BeGreaterThan(0, "data_handling directive must be present");
        contextTemplateIndex.Should().BeGreaterThan(
            dataHandlingIndex,
            "data_handling must sit inside the static_system_prompt block, before the context_template boundary");

        // The directive should be near the END of the system block - assert no other major
        // top-level sections (uppercase headers like 'SAFETY:' or 'TOPIC SCHEMA') follow it.
        var afterDirective = promptText.Substring(dataHandlingIndex, contextTemplateIndex - dataHandlingIndex);
        afterDirective.Should().NotContain("TOPIC SCHEMA", "data_handling must follow the topic schema, not precede it");
        afterDirective.Should().NotContain("SAFETY:", "data_handling must follow the safety section, not precede it");
    }

    [Fact]
    public void OnboardingV1Yaml_DocumentsAllSixTopics()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);
        var expectedTopics = new[]
        {
            "PrimaryGoal",
            "TargetEvent",
            "CurrentFitness",
            "WeeklySchedule",
            "InjuryHistory",
            "Preferences",
        };

        // Act + Assert
        foreach (var expected in expectedTopics)
        {
            promptText.Should().Contain(
                expected,
                $"the onboarding prompt must describe the '{expected}' topic in the topic schema");
        }
    }

    private static string ResolvePromptFile()
    {
        // Walk up from the test assembly location until we find the backend repo root, then
        // resolve the prompt file under src/RunCoach.Api/Prompts/.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "RunCoach.Api",
                "Prompts",
                "onboarding-v1.yaml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/RunCoach.Api/Prompts/onboarding-v1.yaml by walking up from the test assembly.");
    }
}
