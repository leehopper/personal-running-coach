using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching.Prompts;

namespace RunCoach.Api.Tests.Modules.Coaching.Adaptation;

/// <summary>
/// Trademark + structural assertions on the on-disk adaptation prompt YAML (Slice 3 Unit 4).
/// Per DEC-042 the user-facing surface must use "Daniels-Gilbert zones" / "pace-zone index",
/// never the trademarked term VDOT. The prompt consumes the ContextAssembler context, the
/// DeviationResult, and the resolved EscalationLevel; recent-log notes (user-controlled) are
/// framed in SECTION_NAME spotlighting delimiters per R-068 / DEC-059. The dot-named
/// <c>adaptation.v1.yaml</c> + the <c>adaptation</c> active version let the orchestration
/// layer load it through the prompt store the same way coaching-system loads.
/// </summary>
public sealed class AdaptationPromptTests
{
    private const string PromptId = "adaptation";
    private const string PromptVersion = "v1";

    private static string PromptFilePath => ResolvePromptFile();

    [Fact]
    public void AdaptationV1Yaml_FileExists()
    {
        // Arrange
        var promptFilePath = PromptFilePath;

        // Act
        var actualExists = File.Exists(promptFilePath);

        // Assert
        actualExists.Should().BeTrue($"the adaptation prompt file must exist at '{promptFilePath}'");
    }

    [Fact]
    public void AdaptationV1Yaml_ContainsNoVdotTrademark()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualContainsTrademark = promptText.Contains("vdot", StringComparison.OrdinalIgnoreCase);

        // Assert
        actualContainsTrademark.Should().BeFalse(
            "the adaptation prompt must not reference the trademarked term VDOT (DEC-042 / NOTICE)");
    }

    [Fact]
    public void AdaptationV1Yaml_UsesDanielsGilbertVocabulary()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualHasGenericVocab = promptText.Contains("Daniels-Gilbert zones", StringComparison.Ordinal)
            || promptText.Contains("pace-zone index", StringComparison.Ordinal);

        // Assert
        actualHasGenericVocab.Should().BeTrue(
            "the adaptation prompt must use 'Daniels-Gilbert zones' or 'pace-zone index' for pace methodology");
    }

    [Fact]
    public void AdaptationV1Yaml_DefinesSystemPromptAndContextTemplateBlocks()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualHasSystemBlock = promptText.Contains("static_system_prompt:", StringComparison.Ordinal);
        var actualHasContextTemplate = promptText.Contains("context_template:", StringComparison.Ordinal);

        // Assert
        actualHasSystemBlock.Should().BeTrue("the adaptation prompt must define a cacheable system block");
        actualHasContextTemplate.Should().BeTrue("the adaptation prompt must define a context template for per-call injection");
    }

    [Theory]
    [InlineData("{{plan_context}}")]
    [InlineData("{{recent_logs}}")]
    [InlineData("{{deviation_summary}}")]
    [InlineData("{{escalation_level}}")]
    [InlineData("{{safety_tier}}")]
    public void AdaptationV1Yaml_ContextTemplateConsumesAdaptationInputs(string token)
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualContextBlock = GetAnchorBlock(promptText, "context_template:");

        // Assert
        actualContextBlock.Should().Contain(
            token,
            $"the context_template must consume {token} (ContextAssembler context + DeviationResult + resolved EscalationLevel)");
    }

    [Fact]
    public void AdaptationV1Yaml_FramesUserControlledRecentLogsInSpotlightingDelimiter()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualContextBlock = GetAnchorBlock(promptText, "context_template:");

        // Assert — recent-log notes/metrics are user-controlled free text that reaches an LLM
        // which can change a plan; they must land inside a nonce-framed SECTION_NAME boundary (R-068).
        actualContextBlock.Should().Contain(
            "<SECTION_NAME id=\"{{recent_logs_nonce}}\">",
            "the context_template must open a nonce-framed SECTION_NAME around the user-controlled recent logs");
        actualContextBlock.Should().Contain(
            "</SECTION_NAME>",
            "the context_template must close every SECTION_NAME tag opened around user-controlled content");
    }

    [Fact]
    public void AdaptationV1Yaml_CarriesDataHandlingDirective()
    {
        // Arrange
        var promptText = File.ReadAllText(PromptFilePath);

        // Act
        var actualHasDirective = promptText.Contains("data_handling", StringComparison.Ordinal)
            && promptText.Contains("SECTION_NAME", StringComparison.Ordinal);

        // Assert
        actualHasDirective.Should().BeTrue(
            "the adaptation prompt must carry the R-068 / DEC-059 data_handling directive referencing SECTION_NAME delimiters");
    }

    [Fact]
    public async Task AdaptationV1Yaml_LoadsThroughThePromptStore()
    {
        // Arrange — proves the dot-naming convention + the registered active version so the
        // orchestration layer can resolve it via GetActiveVersion/GetPromptAsync.
        var promptsDir = Path.GetDirectoryName(PromptFilePath)!;
        var settings = new PromptStoreSettings
        {
            BasePath = "Prompts",
            ActiveVersions = new Dictionary<string, string> { [PromptId] = PromptVersion },
        };
        var store = new YamlPromptStore(settings, promptsDir, NullLogger<YamlPromptStore>.Instance);

        // Act
        var actualActiveVersion = store.GetActiveVersion(PromptId);
        var actualTemplate = await store.GetPromptAsync(PromptId, actualActiveVersion, TestContext.Current.CancellationToken);

        // Assert
        actualActiveVersion.Should().Be(PromptVersion);
        actualTemplate.StaticSystemPrompt.Should().NotBeNullOrWhiteSpace();
        actualTemplate.ContextTemplate.Should().NotBeNullOrWhiteSpace();
    }

    private static string GetAnchorBlock(string promptText, string anchor)
    {
        var anchorStart = promptText.IndexOf(anchor, StringComparison.Ordinal);
        anchorStart.Should().BeGreaterThanOrEqualTo(
            0,
            because: $"the adaptation prompt must define a '{anchor}' block — without it, structural assertions can't proceed");

        // Bound the returned text to this top-level YAML block so a token assertion cannot pass
        // on a match that lives in a later section. The block runs from the anchor key to the
        // next top-level key (a newline immediately followed by a non-whitespace, column-0
        // character) or EOF; block-scalar content lines are all indented, so only a sibling
        // top-level key terminates the block.
        var remainder = promptText[anchorStart..];
        var nextTopLevelKey = Regex.Match(remainder, @"\n(?=\S)");
        return nextTopLevelKey.Success ? remainder[..nextTopLevelKey.Index] : remainder;
    }

    private static string ResolvePromptFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "RunCoach.Api",
                "Prompts",
                "adaptation.v1.yaml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate src/RunCoach.Api/Prompts/adaptation.v1.yaml by walking up from the test assembly.");
    }
}
