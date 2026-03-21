using System.Collections.Immutable;
using System.Globalization;
using RunCoach.Api.Modules.Coaching.Models;

namespace RunCoach.Api.Modules.Coaching.Experiments;

/// <summary>
/// A parameterized context assembler for experiments that wraps the standard
/// <see cref="ContextAssembler"/> and applies experiment-specific configuration.
///
/// Supports configurable:
/// - Token budgets (8K, 12K, 15K)
/// - Profile positional placement (start, middle, end)
/// - Training history summarization mode (per-workout, weekly, mixed)
/// - Conversation history turn counts
///
/// This is NOT a replacement for the standard ContextAssembler. It delegates
/// to a ContextAssembler instance and post-processes the result to apply
/// experiment variations.
/// </summary>
public sealed class ExperimentContextAssembler
{
    private readonly ContextAssembler _assembler = new();

    /// <summary>
    /// Gets the inner assembler for direct token estimation access.
    /// </summary>
    internal ContextAssembler InnerAssembler => _assembler;

    /// <summary>
    /// Assembles a prompt payload using the given experiment configuration.
    /// The configuration controls token budget, section ordering, summarization
    /// mode, and conversation history.
    /// </summary>
    /// <param name="input">The standard assembler input data.</param>
    /// <param name="config">The experiment configuration to apply.</param>
    /// <returns>The assembled prompt with experiment-specific section ordering.</returns>
    public AssembledPrompt Assemble(ContextAssemblerInput input, ExperimentConfig config)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(config);

        // Apply conversation turn limit from config.
        var conversationHistory = TruncateConversation(input.ConversationHistory, config.ConversationTurns);
        var adjustedInput = input with { ConversationHistory = conversationHistory };

        // Get the base assembly.
        var baseResult = _assembler.Assemble(adjustedInput);

        // Apply positional placement if different from default (start).
        if (config.ProfilePlacement != ProfilePlacement.Start)
        {
            baseResult = ReorderSections(baseResult, config.ProfilePlacement);
        }

        // Apply summarization mode if different from default (mixed).
        if (config.SummarizationMode == SummarizationMode.WeeklySummaryOnly)
        {
            baseResult = ApplyWeeklySummaryOnly(adjustedInput, baseResult);
        }
        else if (config.SummarizationMode == SummarizationMode.PerWorkoutOnly)
        {
            baseResult = ApplyPerWorkoutOnly(adjustedInput, baseResult);
        }

        // Recalculate token total.
        var totalTokens = _assembler.EstimateTokens(baseResult.SystemPrompt)
            + baseResult.StartSections.Sum(s => s.EstimatedTokens)
            + baseResult.MiddleSections.Sum(s => s.EstimatedTokens)
            + baseResult.EndSections.Sum(s => s.EstimatedTokens);

        // Apply token budget enforcement.
        if (totalTokens > config.TotalTokenBudget)
        {
            baseResult = EnforceBudget(baseResult, config.TotalTokenBudget, totalTokens);
            totalTokens = _assembler.EstimateTokens(baseResult.SystemPrompt)
                + baseResult.StartSections.Sum(s => s.EstimatedTokens)
                + baseResult.MiddleSections.Sum(s => s.EstimatedTokens)
                + baseResult.EndSections.Sum(s => s.EstimatedTokens);
        }

        return baseResult with { EstimatedTokenCount = totalTokens };
    }

    /// <summary>
    /// Truncates conversation history to the specified number of turns.
    /// Takes the most recent turns (from the end of the array).
    /// </summary>
    private static ImmutableArray<ConversationTurn> TruncateConversation(
        ImmutableArray<ConversationTurn> history,
        int maxTurns)
    {
        if (maxTurns <= 0 || history.Length == 0)
        {
            return ImmutableArray<ConversationTurn>.Empty;
        }

        return history.Length <= maxTurns
            ? history
            : history[^maxTurns..];
    }

    /// <summary>
    /// Reorders sections to place profile data in a different positional zone.
    /// Extracts profile-related sections (user_profile, goal_state, fitness_estimate)
    /// from their current position and places them in the target zone.
    /// </summary>
    private static AssembledPrompt ReorderSections(AssembledPrompt result, ProfilePlacement placement)
    {
        var profileKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "user_profile",
            "goal_state",
            "fitness_estimate",
        };

        // Extract profile sections from start.
        var profileSections = result.StartSections
            .Where(s => profileKeys.Contains(s.Key))
            .ToImmutableArray();

        var remainingStart = result.StartSections
            .Where(s => !profileKeys.Contains(s.Key))
            .ToImmutableArray();

        return placement switch
        {
            ProfilePlacement.Middle => result with
            {
                StartSections = remainingStart,
                MiddleSections = [.. profileSections, .. result.MiddleSections],
            },
            ProfilePlacement.End => result with
            {
                StartSections = remainingStart,
                EndSections = [.. profileSections, .. result.EndSections],
            },
            _ => result,
        };
    }

    /// <summary>
    /// Enforces the token budget by progressively removing content.
    /// Removes middle sections first, then truncates end sections.
    /// </summary>
    private static AssembledPrompt EnforceBudget(AssembledPrompt result, int budget, int currentTokens)
    {
        var working = result;
        var tokens = currentTokens;

        // Step 1: Remove middle sections.
        if (tokens > budget && working.MiddleSections.Length > 0)
        {
            working = working with { MiddleSections = ImmutableArray<PromptSection>.Empty };
            tokens = CalculateTotal(working);
        }

        // Step 2: Trim conversation history from end sections.
        if (tokens > budget)
        {
            var trimmedEnd = working.EndSections
                .Where(s => s.Key == "current_user_message")
                .ToImmutableArray();

            working = working with { EndSections = trimmedEnd };
        }

        return working;
    }

    /// <summary>
    /// Builds weekly summary text from workout history (Layer 2 format).
    /// </summary>
    private static string BuildWeeklySummaryText(ImmutableArray<Training.Models.WorkoutSummary> history)
    {
        var weeks = history
            .GroupBy(w => (ISOWeek.GetYear(w.Date.ToDateTime(TimeOnly.MinValue)) * 100)
                + ISOWeek.GetWeekOfYear(w.Date.ToDateTime(TimeOnly.MinValue)))
            .OrderByDescending(g => g.Key)
            .Take(4);

        var lines = new List<string>();
        foreach (var week in weeks)
        {
            var workouts = week.OrderBy(w => w.Date).ToList();
            var weekStart = workouts[0].Date;
            var totalKm = workouts.Sum(w => w.DistanceKm);
            var runCount = workouts.Count;
            var longRun = workouts
                .Where(w => w.WorkoutType == "LongRun")
                .Select(w => w.DistanceKm)
                .FirstOrDefault();

            var longRunPart = longRun > 0 ? $" | Long run: {longRun} km" : string.Empty;
            lines.Add($"Week of {weekStart:yyyy-MM-dd}: {totalKm} km total | {runCount} runs{longRunPart}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Builds per-workout detail text from workout history (Layer 1 format).
    /// </summary>
    private static string BuildPerWorkoutText(ImmutableArray<Training.Models.WorkoutSummary> history)
    {
        var lines = history
            .OrderByDescending(w => w.Date)
            .Select(w =>
            {
                var notes = string.IsNullOrWhiteSpace(w.Notes) ? string.Empty : $" | {w.Notes}";
                var pace = w.AveragePacePerKm.TotalHours >= 1
                    ? w.AveragePacePerKm.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                    : w.AveragePacePerKm.ToString(@"m\:ss", CultureInfo.InvariantCulture);
                return $"{w.Date:yyyy-MM-dd} | {w.WorkoutType} | {w.DistanceKm} km | {w.DurationMinutes} min | {pace}/km{notes}";
            });

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Calculates total tokens across all sections including system prompt.
    /// Uses character ratio estimation (same as the standard assembler).
    /// </summary>
    private static int CalculateTotal(AssembledPrompt result)
    {
        var systemTokens = EstimateTokensStatic(result.SystemPrompt);
        return systemTokens
            + result.StartSections.Sum(s => s.EstimatedTokens)
            + result.MiddleSections.Sum(s => s.EstimatedTokens)
            + result.EndSections.Sum(s => s.EstimatedTokens);
    }

    /// <summary>
    /// Static token estimation matching the ContextAssembler formula.
    /// </summary>
    private static int EstimateTokensStatic(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var rawEstimate = (double)text.Length / ContextAssembler.CharsPerToken;
        var withMargin = rawEstimate * (1.0 + ContextAssembler.SafetyMarginPercent);
        return (int)Math.Ceiling(withMargin);
    }

    /// <summary>
    /// Replaces the training history section with weekly-summary-only content.
    /// </summary>
    private AssembledPrompt ApplyWeeklySummaryOnly(ContextAssemblerInput input, AssembledPrompt result)
    {
        if (input.TrainingHistory.Length == 0)
        {
            return result;
        }

        var weeklySummaryContent = BuildWeeklySummaryText(input.TrainingHistory);
        var tokens = _assembler.EstimateTokens(weeklySummaryContent);
        var summarySection = new PromptSection("training_history", weeklySummaryContent, tokens);

        var newMiddle = result.MiddleSections
            .Where(s => s.Key != "training_history")
            .Append(summarySection)
            .ToImmutableArray();

        return result with { MiddleSections = newMiddle };
    }

    /// <summary>
    /// Replaces the training history section with per-workout-only content.
    /// </summary>
    private AssembledPrompt ApplyPerWorkoutOnly(ContextAssemblerInput input, AssembledPrompt result)
    {
        if (input.TrainingHistory.Length == 0)
        {
            return result;
        }

        var perWorkoutContent = BuildPerWorkoutText(input.TrainingHistory);
        var tokens = _assembler.EstimateTokens(perWorkoutContent);
        var detailSection = new PromptSection("training_history", perWorkoutContent, tokens);

        var newMiddle = result.MiddleSections
            .Where(s => s.Key != "training_history")
            .Append(detailSection)
            .ToImmutableArray();

        return result with { MiddleSections = newMiddle };
    }
}
