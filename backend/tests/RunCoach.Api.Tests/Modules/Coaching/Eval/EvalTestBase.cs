using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Configuration;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Base class for eval tests that use M.E.AI.Evaluation caching infrastructure.
/// Provides cached IChatClient wrappers for both Sonnet (plan generation) and
/// Haiku (LLM-as-judge) calls, plus helpers for loading profiles and writing results.
///
/// Response caching means unchanged prompts serve cached responses (zero cost, instant).
/// Only prompt changes trigger live API calls.
///
/// All derived test classes must use [Trait("Category", "Eval")] to exclude
/// from normal CI runs. These tests require a live API key.
/// </summary>
public abstract class EvalTestBase : IAsyncDisposable
{
    private const string EvalResultsDir = "poc1-eval-results";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CoachingLlmSettings _settings;
    private readonly ReportingConfiguration? _sonnetReportingConfig;
    private readonly ReportingConfiguration? _haikuReportingConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvalTestBase"/> class.
    /// Reads Anthropic API key from user-secrets or environment variables,
    /// creates M.E.AI.Evaluation caching infrastructure for Sonnet and Haiku.
    /// </summary>
    protected EvalTestBase()
    {
        _settings = LoadSettings();
        Assembler = new ContextAssembler();

        if (!IsApiKeyConfigured)
        {
            return;
        }

        var anthropicClient = new AnthropicClient(new ClientOptions
        {
            ApiKey = _settings.ApiKey,
            MaxRetries = _settings.MaxRetries,
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds),
        });

        // Sonnet client for plan generation and coaching narrative.
        // AnthropicStructuredOutputClient intercepts ForJsonSchema requests and
        // delegates to the native SDK's constrained decoding (OutputConfig.JsonOutputFormat).
        // Unstructured calls pass through to the standard IChatClient bridge.
        IChatClient sonnetInner = anthropicClient.AsIChatClient(
            _settings.ModelId, _settings.MaxTokens);
        IChatClient sonnetClient = new AnthropicStructuredOutputClient(
            sonnetInner, anthropicClient, _settings.ModelId, _settings.MaxTokens);
        _sonnetReportingConfig = DiskBasedReportingConfiguration.Create(
            storageRootPath: GetCacheStoragePath("sonnet"),
            evaluators: [],
            chatConfiguration: new ChatConfiguration(sonnetClient),
            enableResponseCaching: true,
            executionName: "eval");

        // Haiku client for LLM-as-judge calls (unstructured only, no wrapper needed)
        IChatClient haikuClient = anthropicClient.AsIChatClient(
            _settings.JudgeModelId, 1024);
        _haikuReportingConfig = DiskBasedReportingConfiguration.Create(
            storageRootPath: GetCacheStoragePath("haiku"),
            evaluators: [],
            chatConfiguration: new ChatConfiguration(haikuClient),
            enableResponseCaching: true,
            executionName: "eval");

        EnsureOutputDirectory();
    }

    /// <summary>
    /// Gets a value indicating whether gets whether the API key is configured.
    /// Tests should skip gracefully when this is false.
    /// </summary>
    protected bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    /// <summary>
    /// Gets the context assembler for building prompt payloads.
    /// </summary>
    protected ContextAssembler Assembler { get; }

    /// <summary>
    /// Gets the coaching LLM settings (model IDs, temperature, etc.).
    /// </summary>
    protected CoachingLlmSettings Settings => _settings;

    /// <summary>
    /// Loads a named test profile by key (sarah, lee, maria, james, priya).
    /// </summary>
    public static TestProfile LoadProfile(string name)
    {
        if (!TestProfiles.All.TryGetValue(name, out var profile))
        {
            var validNames = string.Join(", ", TestProfiles.All.Keys);
            throw new ArgumentException(
                $"Unknown profile '{name}'. Valid profiles: {validNames}",
                nameof(name));
        }

        return profile;
    }

    /// <summary>
    /// Writes the full eval result (LLM response and metadata) to a JSON file
    /// in the poc1-eval-results/ directory.
    /// </summary>
    public static void WriteEvalResult(string scenarioName, object result)
    {
        EnsureOutputDirectory();
        var outputPath = GetOutputPath(scenarioName);
        var json = JsonSerializer.Serialize(result, WriteOptions);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Gets the absolute path for the eval results output directory.
    /// </summary>
    public static string GetOutputDirectory()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        return Path.Combine(backendDir, EvalResultsDir);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds the full user message text from the assembled prompt sections.
    /// </summary>
    protected static string BuildUserMessageFromSections(AssembledPrompt assembled)
    {
        var parts = new List<string>();

        foreach (var section in assembled.StartSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        foreach (var section in assembled.MiddleSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        foreach (var section in assembled.EndSections)
        {
            parts.Add($"=== {section.Key.ToUpperInvariant()} ===\n{section.Content}");
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Creates a cached Sonnet scenario run for plan generation / coaching tests.
    /// The returned <see cref="ScenarioRun"/> provides a cache-wrapped IChatClient
    /// via <c>run.ChatConfiguration!.ChatClient</c>.
    /// </summary>
    protected async ValueTask<ScenarioRun> CreateSonnetScenarioRunAsync(string scenarioName)
    {
        if (_sonnetReportingConfig is null)
        {
            throw new InvalidOperationException("Sonnet client not initialized — API key not configured.");
        }

        return await _sonnetReportingConfig.CreateScenarioRunAsync(scenarioName);
    }

    /// <summary>
    /// Creates a cached Haiku scenario run for LLM-as-judge calls.
    /// The returned <see cref="ScenarioRun"/> provides a cache-wrapped IChatClient
    /// via <c>run.ChatConfiguration!.ChatClient</c>.
    /// </summary>
    protected async ValueTask<ScenarioRun> CreateHaikuScenarioRunAsync(string scenarioName)
    {
        if (_haikuReportingConfig is null)
        {
            throw new InvalidOperationException("Haiku client not initialized — API key not configured.");
        }

        return await _haikuReportingConfig.CreateScenarioRunAsync(scenarioName);
    }

    /// <summary>
    /// Assembles a full prompt payload from a test profile and optional user message.
    /// </summary>
    protected AssembledPrompt AssembleContext(TestProfile profile, string? userMessage = null)
    {
        var message = userMessage ?? BuildDefaultUserMessage(profile);

        var input = new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            ImmutableArray<ConversationTurn>.Empty,
            message);

        return Assembler.Assemble(input);
    }

    /// <summary>
    /// Assembles context with conversation history for safety boundary tests.
    /// </summary>
    protected AssembledPrompt AssembleContextWithConversation(
        TestProfile profile,
        ImmutableArray<ConversationTurn> conversationHistory,
        string currentMessage)
    {
        var input = new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            conversationHistory,
            currentMessage);

        return Assembler.Assemble(input);
    }

    /// <summary>
    /// Disposes managed resources. Override in derived classes for cleanup.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }

    private static string GetCacheStoragePath(string clientName)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        return Path.Combine(backendDir, "poc1-eval-cache", clientName);
    }

    private static string BuildDefaultUserMessage(TestProfile profile)
    {
        var goalDescription = profile.GoalState.TargetRace is not null
            ? $"a {profile.GoalState.TargetRace.Distance} ({profile.GoalState.TargetRace.RaceName})"
            : $"a {profile.GoalState.GoalType} plan";

        return $"""
            I'm {profile.UserProfile.Name}. Please generate a complete training plan for me.
            I'm training for {goalDescription}.

            Please provide a comprehensive periodized training plan with coaching rationale.
            """;
    }

    private static CoachingLlmSettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<EvalTestBase>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = new CoachingLlmSettings();
        configuration.GetSection(CoachingLlmSettings.SectionName).Bind(settings);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            if (!string.IsNullOrWhiteSpace(envKey))
            {
                settings = settings with { ApiKey = envKey };
            }
        }

        return settings;
    }

    private static void EnsureOutputDirectory()
    {
        var dir = GetOutputDirectory();
        Directory.CreateDirectory(dir);
    }

    private static string GetOutputPath(string scenarioName)
    {
        return Path.Combine(GetOutputDirectory(), $"{scenarioName}.json");
    }
}
