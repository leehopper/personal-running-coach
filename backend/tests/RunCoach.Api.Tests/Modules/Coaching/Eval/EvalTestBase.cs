using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Training.Models;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Base class for eval tests that call the real Anthropic API.
/// Provides helpers for loading profiles, assembling context, calling the LLM,
/// writing eval results to disk, and parsing JSON plan structures from responses.
///
/// All derived test classes must use [Trait("Category", "Eval")] to exclude
/// from normal CI runs. These tests require a live API key.
/// </summary>
public abstract class EvalTestBase : IDisposable
{
    /// <summary>
    /// The output directory for eval results (relative to the backend/ directory).
    /// </summary>
    private const string EvalResultsDir = "poc1-eval-results";

    /// <summary>
    /// JSON serializer options used for writing eval result files.
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ClaudeCoachingLlm _llm;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvalTestBase"/> class.
    /// Reads Anthropic API key from user-secrets or environment variables,
    /// creates a real LLM client and ContextAssembler.
    /// </summary>
    protected EvalTestBase()
    {
        var settings = LoadSettings();

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is not configured for eval tests. " +
                "Set the 'Anthropic:ApiKey' value via user-secrets: " +
                "dotnet user-secrets set \"Anthropic:ApiKey\" \"<your-key>\" " +
                "--project tests/RunCoach.Api.Tests");
        }

        _llm = new ClaudeCoachingLlm(
            Options.Create(settings),
            NullLogger<ClaudeCoachingLlm>.Instance);

        Assembler = new ContextAssembler();

        EnsureOutputDirectory();
    }

    /// <summary>
    /// Gets the context assembler for building prompt payloads.
    /// </summary>
    protected ContextAssembler Assembler { get; }

    /// <summary>
    /// Loads a named test profile by key (sarah, lee, maria, james, priya).
    /// </summary>
    /// <param name="name">The profile name (case-insensitive).</param>
    /// <returns>The loaded test profile.</returns>
    /// <exception cref="ArgumentException">Thrown when the profile name is not found.</exception>
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
    /// <param name="scenarioName">Name for the output file (e.g., "sarah-plan", "safety-medical").</param>
    /// <param name="result">The result object to serialize (typically includes response, profile, assertions).</param>
    public static void WriteEvalResult(string scenarioName, object result)
    {
        EnsureOutputDirectory();
        var outputPath = GetOutputPath(scenarioName);
        var json = JsonSerializer.Serialize(result, WriteOptions);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Writes the raw LLM response along with metadata to a JSON file.
    /// </summary>
    /// <param name="scenarioName">Name for the output file.</param>
    /// <param name="profileName">The profile name used in this eval.</param>
    /// <param name="llmResponse">The raw text response from the LLM.</param>
    /// <param name="estimatedTokens">The estimated token count of the assembled prompt.</param>
    public static void WriteEvalResult(
        string scenarioName,
        string profileName,
        string llmResponse,
        int estimatedTokens)
    {
        var result = new
        {
            Scenario = scenarioName,
            Profile = profileName,
            Timestamp = DateTime.UtcNow.ToString("o"),
            EstimatedPromptTokens = estimatedTokens,
            Response = llmResponse,
        };

        WriteEvalResult(scenarioName, result);
    }

    /// <summary>
    /// Extracts the first JSON code block from the LLM response text.
    /// Returns null if no JSON block is found.
    /// </summary>
    /// <param name="response">The raw LLM response.</param>
    /// <returns>The extracted JSON string, or null.</returns>
    public static string? ExtractJsonBlock(string response)
    {
        const string jsonStart = "```json";
        const string codeEnd = "```";

        var startIndex = response.IndexOf(jsonStart, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0)
        {
            return null;
        }

        var contentStart = startIndex + jsonStart.Length;
        var endIndex = response.IndexOf(codeEnd, contentStart, StringComparison.OrdinalIgnoreCase);

        return endIndex < 0 ? null : response[contentStart..endIndex].Trim();
    }

    /// <summary>
    /// Attempts to parse a JSON block within the LLM response into a JsonElement.
    /// Returns null if no JSON block is found or parsing fails.
    /// </summary>
    /// <param name="response">The raw LLM response.</param>
    /// <returns>The parsed JSON root element, or null.</returns>
    public static JsonElement? ParsePlanJson(string response)
    {
        var jsonBlock = ExtractJsonBlock(response);

        if (jsonBlock is null)
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(jsonBlock).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts a MacroPlan section from the parsed JSON response.
    /// Looks for common key patterns: "macroPlan", "macro_plan", "MacroPlan".
    /// </summary>
    /// <param name="json">The parsed JSON root element.</param>
    /// <returns>The macro plan element, or null if not found.</returns>
    public static JsonElement? ExtractMacroPlan(JsonElement json)
    {
        return TryGetProperty(json, "macroPlan", "macro_plan", "MacroPlan", "plan");
    }

    /// <summary>
    /// Extracts a MesoWeek section from the parsed JSON response.
    /// Looks for common key patterns: "mesoWeek", "meso_week", "MesoWeek", "weekTemplate".
    /// </summary>
    /// <param name="json">The parsed JSON root element.</param>
    /// <returns>The meso week element, or null if not found.</returns>
    public static JsonElement? ExtractMesoWeek(JsonElement json)
    {
        return TryGetProperty(json, "mesoWeek", "meso_week", "MesoWeek", "weekTemplate", "week_template");
    }

    /// <summary>
    /// Extracts MicroWorkout entries from the parsed JSON response.
    /// Looks for common key patterns: "microWorkouts", "micro_workouts", "workouts".
    /// </summary>
    /// <param name="json">The parsed JSON root element.</param>
    /// <returns>The micro workouts array element, or null if not found.</returns>
    public static JsonElement? ExtractMicroWorkouts(JsonElement json)
    {
        return TryGetProperty(json, "microWorkouts", "micro_workouts", "MicroWorkouts", "workouts");
    }

    /// <summary>
    /// Gets the absolute path for the eval results output directory.
    /// </summary>
    /// <returns>The absolute path to the poc1-eval-results directory.</returns>
    public static string GetOutputDirectory()
    {
        // Navigate from the test assembly location up to the backend/ directory.
        var assemblyDir = Path.GetDirectoryName(typeof(EvalTestBase).Assembly.Location)!;
        var backendDir = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", ".."));
        return Path.Combine(backendDir, EvalResultsDir);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Assembles a full prompt payload from a test profile and optional user message.
    /// Uses the profile's data with no conversation history by default.
    /// </summary>
    /// <param name="profile">The test profile to assemble context for.</param>
    /// <param name="userMessage">The user message to include. Defaults to a plan generation request.</param>
    /// <returns>The assembled prompt ready for LLM call.</returns>
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
    /// <param name="profile">The test profile to use as base context.</param>
    /// <param name="conversationHistory">Prior conversation turns.</param>
    /// <param name="currentMessage">The current user message to test.</param>
    /// <returns>The assembled prompt ready for LLM call.</returns>
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
    /// Calls the real Anthropic LLM with the assembled prompt.
    /// </summary>
    /// <param name="assembled">The assembled prompt payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw LLM response text.</returns>
    protected async Task<string> CallLlmAsync(AssembledPrompt assembled, CancellationToken ct = default)
    {
        var userMessage = BuildUserMessageFromSections(assembled);
        return await _llm.GenerateAsync(assembled.SystemPrompt, userMessage, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _llm.Dispose();
        }
    }

    /// <summary>
    /// Builds the full user message text from the assembled prompt sections.
    /// Follows the same pattern as the console app.
    /// </summary>
    private static string BuildUserMessageFromSections(AssembledPrompt assembled)
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
    /// Builds the default plan generation request message for a profile.
    /// </summary>
    private static string BuildDefaultUserMessage(TestProfile profile)
    {
        var goalDescription = profile.GoalState.TargetRace is not null
            ? $"a {profile.GoalState.TargetRace.Distance} ({profile.GoalState.TargetRace.RaceName})"
            : $"a {profile.GoalState.GoalType} plan";

        return $"""
            I'm {profile.UserProfile.Name}. Please generate a complete training plan for me.
            I'm training for {goalDescription}.

            Please provide:
            1. A MacroPlan with phased periodization
            2. A MesoWeek template for the current week
            3. MicroWorkout details for my next 3 training days

            Respond with the plan as a JSON object in a ```json code fence, followed by coaching notes.
            """;
    }

    /// <summary>
    /// Loads the CoachingLlmSettings from user-secrets and environment variables.
    /// Uses the test project's UserSecretsId to locate secrets.
    /// </summary>
    private static CoachingLlmSettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<EvalTestBase>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var settings = new CoachingLlmSettings();
        configuration.GetSection(CoachingLlmSettings.SectionName).Bind(settings);

        // Also check for a direct environment variable as fallback.
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

    /// <summary>
    /// Ensures the poc1-eval-results/ output directory exists.
    /// </summary>
    private static void EnsureOutputDirectory()
    {
        var dir = GetOutputDirectory();
        Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Gets the full file path for an eval result JSON file.
    /// </summary>
    private static string GetOutputPath(string scenarioName)
    {
        return Path.Combine(GetOutputDirectory(), $"{scenarioName}.json");
    }

    /// <summary>
    /// Tries to get a property from a JSON element using multiple possible key names.
    /// Returns the first match, or null if none found.
    /// </summary>
    private static JsonElement? TryGetProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value))
            {
                return value;
            }
        }

        return null;
    }
}
