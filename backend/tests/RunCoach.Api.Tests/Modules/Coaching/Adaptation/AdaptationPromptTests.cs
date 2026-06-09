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
/// layer (Unit 5) load it through the prompt store the same way coaching-system loads.
/// </summary>
public sealed class AdaptationPromptTests
{
    private const string PromptId = "adaptation";
    private const string PromptVersion = "v1";

    private static string PromptFilePath => ResolvePromptFile();

    [Fact]
    public void AdaptationV1Yaml_FileExists()
    {
        File.Exists(PromptFilePath).Should().BeTrue(
            $"the adaptation prompt file must exist at '{PromptFilePath}'");
    }

    [Fact]
    public void AdaptationV1Yaml_ContainsNoVdotTrademark()
    {
        var promptText = File.ReadAllText(PromptFilePath);

        promptText.Contains("vdot", StringComparison.OrdinalIgnoreCase).Should().BeFalse(
            "the adaptation prompt must not reference the trademarked term VDOT (DEC-042 / NOTICE)");
    }

    [Fact]
    public void AdaptationV1Yaml_UsesDanielsGilbertVocabulary()
    {
        var promptText = File.ReadAllText(PromptFilePath);

        var hasGenericVocab = promptText.Contains("Daniels-Gilbert zones", StringComparison.Ordinal)
            || promptText.Contains("pace-zone index", StringComparison.Ordinal);

        hasGenericVocab.Should().BeTrue(
            "the adaptation prompt must use 'Daniels-Gilbert zones' or 'pace-zone index' for pace methodology");
    }

    [Fact]
    public void AdaptationV1Yaml_DefinesSystemPromptAndContextTemplateBlocks()
    {
        var promptText = File.ReadAllText(PromptFilePath);

        promptText.Should().Contain("static_system_prompt:", "the adaptation prompt must define a cacheable system block");
        promptText.Should().Contain("context_template:", "the adaptation prompt must define a context template for per-call injection");
    }

    [Theory]
    [InlineData("{{plan_context}}")]
    [InlineData("{{recent_logs}}")]
    [InlineData("{{deviation_summary}}")]
    [InlineData("{{escalation_level}}")]
    [InlineData("{{safety_tier}}")]
    public void AdaptationV1Yaml_ContextTemplateConsumesAdaptationInputs(string token)
    {
        var promptText = File.ReadAllText(PromptFilePath);
        var contextBlock = GetAnchorBlock(promptText, "context_template:");

        contextBlock.Should().Contain(
            token,
            $"the context_template must consume {token} (ContextAssembler context + DeviationResult + resolved EscalationLevel)");
    }

    [Fact]
    public void AdaptationV1Yaml_FramesUserControlledRecentLogsInSpotlightingDelimiter()
    {
        var promptText = File.ReadAllText(PromptFilePath);
        var contextBlock = GetAnchorBlock(promptText, "context_template:");

        // Recent-log notes/metrics are user-controlled free text that reaches an LLM which can
        // change a plan — they must land inside a nonce-framed SECTION_NAME boundary (R-068).
        contextBlock.Should().Contain(
            "<SECTION_NAME id=\"{{recent_logs_nonce}}\">",
            "the context_template must open a nonce-framed SECTION_NAME around the user-controlled recent logs");
        contextBlock.Should().Contain(
            "</SECTION_NAME>",
            "the context_template must close every SECTION_NAME tag opened around user-controlled content");
    }

    [Fact]
    public void AdaptationV1Yaml_CarriesDataHandlingDirective()
    {
        var promptText = File.ReadAllText(PromptFilePath);

        var hasDirective = promptText.Contains("data_handling", StringComparison.Ordinal)
            && promptText.Contains("SECTION_NAME", StringComparison.Ordinal);

        hasDirective.Should().BeTrue(
            "the adaptation prompt must carry the R-068 / DEC-059 data_handling directive referencing SECTION_NAME delimiters");
    }

    [Fact]
    public async Task AdaptationV1Yaml_LoadsThroughThePromptStore()
    {
        // Proves the dot-naming convention + the registered active version so the
        // orchestration layer (Unit 5) can resolve it via GetActiveVersion/GetPromptAsync.
        var promptsDir = Path.GetDirectoryName(PromptFilePath)!;
        var settings = new PromptStoreSettings
        {
            BasePath = "Prompts",
            ActiveVersions = new Dictionary<string, string> { [PromptId] = PromptVersion },
        };
        var store = new YamlPromptStore(settings, promptsDir, NullLogger<YamlPromptStore>.Instance);

        var activeVersion = store.GetActiveVersion(PromptId);
        var template = await store.GetPromptAsync(PromptId, activeVersion, TestContext.Current.CancellationToken);

        activeVersion.Should().Be(PromptVersion);
        template.StaticSystemPrompt.Should().NotBeNullOrWhiteSpace();
        template.ContextTemplate.Should().NotBeNullOrWhiteSpace();
    }

    private static string GetAnchorBlock(string promptText, string anchor)
    {
        var anchorStart = promptText.IndexOf(anchor, StringComparison.Ordinal);
        anchorStart.Should().BeGreaterThanOrEqualTo(
            0,
            because: $"the adaptation prompt must define a '{anchor}' block — without it, structural assertions can't proceed");
        return promptText[anchorStart..];
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
