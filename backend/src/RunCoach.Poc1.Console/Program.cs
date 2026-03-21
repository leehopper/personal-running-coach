using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RunCoach.Api.Modules.Coaching;
using RunCoach.Api.Modules.Coaching.Models;
using RunCoach.Api.Modules.Coaching.Prompts;
using RunCoach.Api.Modules.Training.Profiles;

namespace RunCoach.Poc1.Console;

/// <summary>
/// Console app for interactive coaching prompt exploration and plan generation.
/// Loads a named test profile, assembles context, calls the LLM, and prints the result.
/// </summary>
public static partial class Program
{
    private static readonly string[] ValidProfiles = ["sarah", "lee", "maria", "james", "priya"];

    public static async Task<int> Main(string[] args)
    {
        // Parse CLI arguments.
        var (profileName, promptVersion, parseError) = ParseArguments(args);

        if (parseError is not null)
        {
            await System.Console.Error.WriteLineAsync(parseError).ConfigureAwait(false);
            return 1;
        }

        // Build host with configuration and DI.
        using var host = BuildHost(args);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RunCoach.Poc1.Console");

        // Validate API key before any work.
        var settings = host.Services.GetRequiredService<IOptions<CoachingLlmSettings>>().Value;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            await System.Console.Error.WriteLineAsync(
                "Error: Anthropic API key is not configured. " +
                "Set the 'Anthropic:ApiKey' value via user-secrets: " +
                "dotnet user-secrets set \"Anthropic:ApiKey\" \"<your-key>\" " +
                "--project src/RunCoach.Poc1.Console").ConfigureAwait(false);
            return 1;
        }

        // Load profile.
        if (!TestProfiles.All.TryGetValue(profileName!, out var profile))
        {
            await System.Console.Error.WriteLineAsync(
                $"Error: Unknown profile '{profileName}'. Valid profiles: {string.Join(", ", ValidProfiles)}").ConfigureAwait(false);
            return 1;
        }

        LogGeneratingPlan(logger, profileName!, promptVersion!);

        // Assemble context using YAML-loaded prompts.
        var assembler = host.Services.GetRequiredService<IContextAssembler>();

        var input = new ContextAssemblerInput(
            profile.UserProfile,
            profile.GoalState,
            profile.GoalState.CurrentFitnessEstimate,
            profile.GoalState.CurrentFitnessEstimate.TrainingPaces,
            profile.TrainingHistory,
            ImmutableArray<ConversationTurn>.Empty,
            BuildUserMessage(profile, promptVersion!));

        var assembled = await assembler.AssembleAsync(input).ConfigureAwait(false);

        var sectionCount = assembled.StartSections.Length
            + assembled.MiddleSections.Length
            + assembled.EndSections.Length;
        LogContextAssembled(logger, assembled.EstimatedTokenCount, sectionCount);

        // Build the user message from all sections.
        var userMessage = BuildUserMessageFromSections(assembled);

        System.Console.WriteLine("=== SYSTEM PROMPT ===");
        System.Console.WriteLine(assembled.SystemPrompt);
        System.Console.WriteLine();
        System.Console.WriteLine("=== USER MESSAGE ===");
        System.Console.WriteLine(userMessage);
        System.Console.WriteLine();
        System.Console.WriteLine($"=== Estimated tokens: {assembled.EstimatedTokenCount} ===");
        System.Console.WriteLine();

        // Call LLM.
        System.Console.WriteLine("=== CALLING LLM ===");
        System.Console.WriteLine();

        var llm = host.Services.GetRequiredService<ICoachingLlm>();

        try
        {
            var response = await llm.GenerateAsync(
                assembled.SystemPrompt,
                userMessage,
                CancellationToken.None).ConfigureAwait(false);

            System.Console.WriteLine("=== LLM RESPONSE ===");
            System.Console.WriteLine(response);
            System.Console.WriteLine();

            // Attempt to extract and display JSON block if present.
            var jsonBlock = ExtractJsonBlock(response);

            if (jsonBlock is not null)
            {
                System.Console.WriteLine("=== EXTRACTED JSON ===");
                System.Console.WriteLine(jsonBlock);
                System.Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogLlmCallFailed(logger, ex);
            await System.Console.Error.WriteLineAsync($"Error: LLM call failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>
    /// Parses CLI arguments for --profile and --prompt-version flags.
    /// Returns (profileName, promptVersion, errorMessage).
    /// </summary>
    internal static (string? ProfileName, string? PromptVersion, string? Error) ParseArguments(string[] args)
    {
        string? profileName = null;
        var promptVersion = "v1";

        var i = 0;
        while (i < args.Length)
        {
            if (args[i] == "--profile" && i + 1 < args.Length)
            {
                profileName = args[i + 1];
                i += 2;
            }
            else if (args[i] == "--prompt-version" && i + 1 < args.Length)
            {
                promptVersion = args[i + 1];
                i += 2;
            }
            else
            {
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            return (null, null, $"Error: --profile is required. Valid profiles: {string.Join(", ", ValidProfiles)}");
        }

        if (!TestProfiles.All.ContainsKey(profileName))
        {
            return (null, null, $"Error: Unknown profile '{profileName}'. Valid profiles: {string.Join(", ", ValidProfiles)}");
        }

        return (profileName, promptVersion, null);
    }

    /// <summary>
    /// Builds the IHost with configuration (appsettings, user-secrets, env vars) and DI services.
    /// </summary>
    internal static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Set content root to the application's base directory so that
        // appsettings.json and Prompts/*.yaml files (copied to output during build)
        // are found regardless of the current working directory.
        builder.Environment.ContentRootPath = AppContext.BaseDirectory;
        builder.Configuration.SetBasePath(AppContext.BaseDirectory);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

        // Add user-secrets in development.
        builder.Configuration.AddUserSecrets<AssemblyMarker>(optional: true);

        // Bind Anthropic settings.
        builder.Services.Configure<CoachingLlmSettings>(
            builder.Configuration.GetSection(CoachingLlmSettings.SectionName));

        // Bind prompt store settings.
        builder.Services.Configure<PromptStoreSettings>(
            builder.Configuration.GetSection(PromptStoreSettings.SectionName));

        // Register prompt store and context assembler with YAML support.
        builder.Services.AddSingleton<IPromptStore, YamlPromptStore>();
        builder.Services.AddSingleton<IContextAssembler, ContextAssembler>();
        builder.Services.AddSingleton<ICoachingLlm, ClaudeCoachingLlm>();

        // Reduce console log noise to warnings only.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("RunCoach", LogLevel.Information);

        return builder.Build();
    }

    /// <summary>
    /// Extracts the first JSON code block from the LLM response, if present.
    /// </summary>
    internal static string? ExtractJsonBlock(string response)
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
    /// Builds the user message requesting plan generation for the given profile.
    /// </summary>
    private static string BuildUserMessage(TestProfile profile, string promptVersion)
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

            Use prompt version: {promptVersion}
            """;
    }

    /// <summary>
    /// Builds the full user message text from the assembled prompt sections,
    /// concatenating start, middle, and end sections with headers.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating plan for profile {ProfileName} with prompt version {PromptVersion}")]
    private static partial void LogGeneratingPlan(ILogger logger, string profileName, string promptVersion);

    [LoggerMessage(Level = LogLevel.Information, Message = "Context assembled: {TokenCount} estimated tokens across {SectionCount} sections")]
    private static partial void LogContextAssembled(ILogger logger, int tokenCount, int sectionCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "LLM call failed")]
    private static partial void LogLlmCallFailed(ILogger logger, Exception exception);
}
