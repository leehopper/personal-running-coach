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
using Microsoft.Extensions.Logging.Abstractions;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Tests.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Base class for eval tests that use M.E.AI.Evaluation caching infrastructure.
/// Provides cached IChatClient wrappers for both Sonnet (plan generation) and
/// Haiku (LLM-as-judge) calls, plus helpers for loading profiles and writing results.
///
/// Response caching means unchanged prompts serve cached responses (zero cost, instant).
/// Only prompt changes trigger live API calls.
///
/// Cache mode is controlled via the EVAL_CACHE_MODE environment variable:
///   - Auto (default): Record when API key available, Replay when not
///   - Record: Live API calls with caching, requires API key
///   - Replay: Cache-only, throws descriptive error on cache miss (for CI)
///
/// All derived test classes must use [Trait("Category", "Eval")].
///
/// <para><b>Re-recording workflow:</b></para>
/// <para>
/// Cache fixtures should be re-recorded when prompts, model IDs, or context assembly
/// logic changes (any change that alters the cache key). To re-record:
/// </para>
/// <list type="number">
///   <item>Set your API key: <c>dotnet user-secrets set "Anthropic:ApiKey" &lt;key&gt;</c></item>
///   <item>Run with Record mode: <c>EVAL_CACHE_MODE=Record dotnet test --filter "Category=Eval"</c></item>
///   <item>Extend TTL on new fixtures: run the TTL extension script (or use a one-liner):
///     <c>find backend/tests/eval-cache -name "entry.json" -exec python3 -c "..." {} \;</c>
///     — set <c>"expiration": "9999-12-31T23:59:59Z"</c> in every entry.json</item>
///   <item>Commit the updated cache directory: <c>git add backend/tests/eval-cache/</c></item>
/// </list>
/// </summary>
public abstract class EvalTestBase : IAsyncDisposable
{
    private const string EvalResultsDir = "eval-results";

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
    /// Reads EVAL_CACHE_MODE and Anthropic API key, then creates the appropriate
    /// M.E.AI.Evaluation caching infrastructure for Sonnet and Haiku.
    /// </summary>
    protected EvalTestBase()
    {
        _settings = LoadSettings();
        CacheMode = ParseCacheMode();
        Assembler = new ContextAssembler(CreatePromptStore(), NullLogger<ContextAssembler>.Instance);

        var effectiveMode = ResolveEffectiveMode(CacheMode, IsApiKeyConfigured);
        TestContext.Current.SendDiagnosticMessage(
            "[EvalTestBase] EVAL_CACHE_MODE={0}, Effective={1}, ApiKeyConfigured={2}",
            CacheMode,
            effectiveMode,
            IsApiKeyConfigured);

        if (effectiveMode == EvalCacheMode.Replay)
        {
            // Replay mode: same client pipeline as Record (for matching cache keys) but with
            // a ReplayGuardChatClient wrapper that throws descriptive errors on cache miss,
            // preventing any outbound network I/O from CI.
            var dummyClient = new AnthropicClient(new ClientOptions
            {
                ApiKey = "replay-mode-no-key",
                MaxRetries = 0,
                Timeout = TimeSpan.FromSeconds(1),
            });

            _sonnetReportingConfig = CreateReplayConfig(
                "sonnet", dummyClient, _settings.ModelId, _settings.MaxTokens);
            _haikuReportingConfig = CreateReplayConfig(
                "haiku", dummyClient, _settings.JudgeModelId, 1024);
        }
        else if (effectiveMode == EvalCacheMode.Record && !IsApiKeyConfigured)
        {
            throw new InvalidOperationException(
                "EVAL_CACHE_MODE=Record requires a valid Anthropic API key. " +
                "Set the key via user-secrets ('dotnet user-secrets set Anthropic:ApiKey <key>') " +
                "or the ANTHROPIC_API_KEY environment variable.");
        }
        else if (IsApiKeyConfigured)
        {
            // Record mode: real API clients with caching.
            var anthropicClient = new AnthropicClient(new ClientOptions
            {
                ApiKey = _settings.ApiKey,
                MaxRetries = _settings.MaxRetries,
                Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds),
            });

            _sonnetReportingConfig = CreateRecordConfig(
                "sonnet", anthropicClient, _settings.ModelId, _settings.MaxTokens);
            _haikuReportingConfig = CreateRecordConfig(
                "haiku", anthropicClient, _settings.JudgeModelId, 1024);
        }

        // If Auto mode and no API key, configs stay null — CanRunEvals is false.
        EnsureOutputDirectory();
    }

    // ── Properties (public → internal → protected) ──────────────────────

    /// <summary>
    /// Gets the active cache mode parsed from the EVAL_CACHE_MODE environment variable.
    /// </summary>
    protected EvalCacheMode CacheMode { get; }

    /// <summary>
    /// Gets a value indicating whether the API key is configured.
    /// In Replay mode, this may be false — tests run from cache without an API key.
    /// </summary>
    protected bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(_settings.ApiKey);

    /// <summary>
    /// Gets a value indicating whether eval tests can execute.
    /// True when an API key is available (Record/Auto) OR when in Replay mode (cache-only).
    /// Tests should use this instead of <see cref="IsApiKeyConfigured"/> to guard eval execution.
    /// </summary>
    protected bool CanRunEvals => _sonnetReportingConfig is not null;

    /// <summary>
    /// Gets the context assembler for building prompt payloads.
    /// </summary>
    protected IContextAssembler Assembler { get; }

    /// <summary>
    /// Gets the coaching LLM settings (model IDs, temperature, etc.).
    /// </summary>
    protected CoachingLlmSettings Settings => _settings;

    // ── Methods (public → internal → protected → private) ───────────────

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
    /// in the eval-results/ directory.
    /// </summary>
    public static async Task WriteEvalResultAsync(string scenarioName, object result)
    {
        EnsureOutputDirectory();
        var outputPath = GetOutputPath(scenarioName);
        var json = JsonSerializer.Serialize(result, WriteOptions);
        await File.WriteAllTextAsync(outputPath, json).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the absolute path for the eval results output directory.
    /// </summary>
    public static string GetOutputDirectory()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        return Path.Combine(backendDir, "tests", EvalResultsDir);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Parses a cache mode value. When <paramref name="envValue"/> is provided, it is used directly;
    /// otherwise the method falls back to the EVAL_CACHE_MODE environment variable.
    /// Case-insensitive, defaults to Auto.
    /// </summary>
    /// <param name="envValue">
    /// Optional cache mode string to parse. Pass explicitly in tests to avoid mutating process state
    /// via <see cref="Environment.SetEnvironmentVariable"/>. When <c>null</c>, the method reads
    /// from the EVAL_CACHE_MODE environment variable (backward-compatible production code path).
    /// </param>
    internal static EvalCacheMode ParseCacheMode(string? envValue = null)
    {
        envValue ??= Environment.GetEnvironmentVariable("EVAL_CACHE_MODE");
        if (string.IsNullOrWhiteSpace(envValue))
        {
            return EvalCacheMode.Auto;
        }

        return Enum.TryParse<EvalCacheMode>(envValue, ignoreCase: true, out var mode)
            ? mode
            : EvalCacheMode.Auto;
    }

    /// <summary>
    /// Resolves the effective mode: Auto becomes Record (if API key) or Replay (if no key).
    /// </summary>
    internal static EvalCacheMode ResolveEffectiveMode(EvalCacheMode mode, bool hasApiKey)
    {
        if (mode != EvalCacheMode.Auto)
        {
            return mode;
        }

        return hasApiKey ? EvalCacheMode.Record : EvalCacheMode.Replay;
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
    /// In Replay mode, the inner client throws on cache miss with the scenario name.
    /// </summary>
    protected async ValueTask<ScenarioRun> CreateSonnetScenarioRunAsync(string scenarioName)
    {
        if (_sonnetReportingConfig is null)
        {
            throw new InvalidOperationException(
                $"Sonnet client not initialized. Mode={CacheMode}, ApiKeyConfigured={IsApiKeyConfigured}. " +
                "In Record mode an API key is required. In Replay mode, cache files must exist.");
        }

        return await _sonnetReportingConfig.CreateScenarioRunAsync(scenarioName);
    }

    /// <summary>
    /// Creates a cached Haiku scenario run for LLM-as-judge calls.
    /// In Replay mode, the inner client throws on cache miss with the scenario name.
    /// </summary>
    protected async ValueTask<ScenarioRun> CreateHaikuScenarioRunAsync(string scenarioName)
    {
        if (_haikuReportingConfig is null)
        {
            throw new InvalidOperationException(
                $"Haiku client not initialized. Mode={CacheMode}, ApiKeyConfigured={IsApiKeyConfigured}. " +
                "In Record mode an API key is required. In Replay mode, cache files must exist.");
        }

        return await _haikuReportingConfig.CreateScenarioRunAsync(scenarioName);
    }

    /// <summary>
    /// Assembles a full prompt payload from a test profile and optional user message.
    /// </summary>
    protected async Task<AssembledPrompt> AssembleContextAsync(
        TestProfile profile,
        string? userMessage = null,
        CancellationToken ct = default)
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

        return await Assembler.AssembleAsync(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Assembles context with conversation history for safety boundary tests.
    /// </summary>
    protected async Task<AssembledPrompt> AssembleContextWithConversationAsync(
        TestProfile profile,
        ImmutableArray<ConversationTurn> conversationHistory,
        string currentMessage,
        CancellationToken ct = default)
    {
        var input = new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            conversationHistory,
            currentMessage);

        return await Assembler.AssembleAsync(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes managed resources. Override in derived classes for cleanup.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        // ReportingConfiguration does not implement IDisposable/IAsyncDisposable.
        // File handle cleanup is managed internally by the M.E.AI.Evaluation library.
        return ValueTask.CompletedTask;
    }

    private static YamlPromptStore CreatePromptStore()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        var promptsDir = Path.Combine(backendDir, "src", "RunCoach.Api", "Prompts");

        var settings = new PromptStoreSettings
        {
            ActiveVersions = new Dictionary<string, string>
            {
                ["coaching-system"] = "v1",
            },
        };

        return new YamlPromptStore(settings, promptsDir, NullLogger<YamlPromptStore>.Instance);
    }

    private static string GetCacheStoragePath(string clientName)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        return Path.Combine(backendDir, "tests", "eval-cache", clientName);
    }

    private static ReportingConfiguration CreateReplayConfig(
        string clientName,
        AnthropicClient dummyAnthropicClient,
        string modelId,
        int maxTokens)
    {
        // Replay mode: build the same client chain as Record mode so the caching layer
        // computes identical cache keys (the M.E.AI cache key includes client metadata).
        // AnthropicStructuredOutputClient wraps the dummy Anthropic client's IChatClient bridge
        // to ensure model ID metadata matches the original recordings.
        // ReplayGuardChatClient sits above the structured client to intercept cache misses
        // with a descriptive error — preventing any outbound network I/O from CI.
        //
        // Pipeline: DiskBasedReportingConfig (caching) → ReplayGuard → StructuredOutput → dummy IChatClient
        IChatClient inner = dummyAnthropicClient.AsIChatClient(modelId, maxTokens);
        IChatClient structuredClient = new AnthropicStructuredOutputClient(
            inner, dummyAnthropicClient, modelId, maxTokens);
        IChatClient guardClient = new ReplayGuardChatClient(structuredClient, clientName);
        return DiskBasedReportingConfiguration.Create(
            storageRootPath: GetCacheStoragePath(clientName),
            evaluators: [],
            chatConfiguration: new ChatConfiguration(guardClient),
            enableResponseCaching: true,
            executionName: "eval");
    }

    private static ReportingConfiguration CreateRecordConfig(
        string clientName,
        AnthropicClient anthropicClient,
        string modelId,
        int maxTokens)
    {
        IChatClient inner = anthropicClient.AsIChatClient(modelId, maxTokens);
        IChatClient client = new AnthropicStructuredOutputClient(
            inner, anthropicClient, modelId, maxTokens);
        return DiskBasedReportingConfiguration.Create(
            storageRootPath: GetCacheStoragePath(clientName),
            evaluators: [],
            chatConfiguration: new ChatConfiguration(client),
            enableResponseCaching: true,
            executionName: "eval");
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
