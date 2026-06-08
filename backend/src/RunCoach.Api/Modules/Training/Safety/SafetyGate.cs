using System.Text.RegularExpressions;
using RunCoach.Api.Modules.Training.Constants;

namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// Deterministic, stateless implementation of <see cref="ISafetyGate"/>. Scans a
/// logged workout's free-text against the versioned
/// <see cref="SafetyKeywordCatalog"/> in escalation-precedence order and returns
/// the highest-precedence <see cref="SafetyClassification"/>. No LLM, no I/O.
/// </summary>
public sealed class SafetyGate : ISafetyGate
{
    private readonly SafetyKeywordCatalog _catalog;

    /// <summary>Initializes a new instance backed by the production catalog.</summary>
    public SafetyGate()
        : this(SafetyKeywordCatalog.Default)
    {
    }

    internal SafetyGate(SafetyKeywordCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <inheritdoc />
    public SafetyClassification Classify(string? notes, IReadOnlyDictionary<string, string>? metrics)
    {
        // Rules are ordered by escalation precedence (crisis, then emergency,
        // then injury, then RED-S), so the first match yields the highest tier.
        foreach (var rule in _catalog.Rules)
        {
            if (!Matches(rule, notes) && !MatchesFreeTextMetrics(rule, metrics))
            {
                continue;
            }

            return rule.Tier == SafetyTier.Red
                ? SafetyClassification.Red(rule.Category)
                : SafetyClassification.Amber(rule.Category);
        }

        return SafetyClassification.Green();
    }

    private static bool MatchesFreeTextMetrics(
        SafetyKeywordCatalog.SafetyRule rule,
        IReadOnlyDictionary<string, string>? metrics)
    {
        if (metrics is null)
        {
            return false;
        }

        // Only the free-text weather and terrain metric values carry user prose.
        // Numeric metrics are not a keyword surface and are skipped.
        return (metrics.TryGetValue(WorkoutMetricKeys.Weather, out var weather) && Matches(rule, weather))
            || (metrics.TryGetValue(WorkoutMetricKeys.Terrain, out var terrain) && Matches(rule, terrain));
    }

    private static bool Matches(SafetyKeywordCatalog.SafetyRule rule, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            return rule.Matcher.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            // ReDoS guard: the note text is uncapped in length. A rule the gate
            // cannot evaluate within the timeout is treated as a match (fail
            // closed) so it escalates rather than silently dropping a possible
            // signal. Under-reaction is the hard-failure mode (DEC-079
            // recall-over-precision); over-reacting on a pathological note is the
            // lesser evil. Preserves the deterministic, no-throw contract.
            return true;
        }
    }
}
