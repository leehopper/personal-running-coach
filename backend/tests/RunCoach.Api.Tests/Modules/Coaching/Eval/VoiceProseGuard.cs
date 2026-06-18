using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Deterministic eval-side voice/style guard (Slice 4A). Serializes an LLM
/// output and asserts no prose field carries a banned style "tell": an em or
/// en dash, an exclamation mark, or a sycophancy/validation phrase. Mirrors
/// <see cref="TrademarkProseGuard"/> — it scans decoded string leaves via
/// <see cref="JsonNode"/> rather than the serialized JSON text. There is no
/// runtime scrubber for these tells, so a fresh fixture that trips this guard
/// means the prompt regressed against the gruff-direct register: tighten the
/// prompt and re-record. ISO dates use U+002D hyphen-minus and are NOT matched.
/// </summary>
internal static class VoiceProseGuard
{
    // Sycophancy / filler-enthusiasm tells flagged on the 2026-06-13 live pass.
    // Case-insensitive substring match. Extended during the tuning rounds.
    internal static readonly string[] BannedPhrases =
    [
        "love it",
        "love that",
        "great foundation",
        "great job",
        "well done",
        "amazing",
        "awesome",
        "fantastic",
        "wonderful",
        "so proud",
    ];

    // U+2014 EM DASH, U+2013 EN DASH. Plain hyphen-minus (U+002D) is allowed.
    private static readonly char[] BannedDashes = ['—', '–'];

    /// <summary>
    /// Returns a description of the first style violation in <paramref name="value"/>,
    /// or <see langword="null"/> when the string is clean.
    /// </summary>
    internal static string? FindViolation(string value)
    {
        if (value.IndexOfAny(BannedDashes) >= 0)
        {
            return "contains an em/en dash";
        }

        if (value.Contains('!'))
        {
            return "contains an exclamation mark";
        }

        foreach (var phrase in BannedPhrases)
        {
            if (value.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return $"contains the banned phrase '{phrase}'";
            }
        }

        return null;
    }

    /// <summary>
    /// Asserts that every string field reachable from <paramref name="output"/> is
    /// free of banned style tells (Slice 4A gruff-direct register).
    /// </summary>
    internal static void AssertClean(string label, object output)
    {
        var node = JsonSerializer.SerializeToNode(output, ClaudeCoachingLlm.StructuredOutputSerializerOptions)
            ?? throw new InvalidOperationException($"Failed to serialize '{label}' to JsonNode.");
        AssertNodeClean(label, node);
    }

    private static void AssertNodeClean(string label, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (_, value) in obj)
                {
                    AssertNodeClean(label, value);
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                {
                    AssertNodeClean(label, item);
                }

                break;
            case JsonValue v when v.TryGetValue<string>(out var s):
                FindViolation(s).Should().BeNull(
                    because: $"every prose field of '{label}' must match the Slice 4A gruff-direct style "
                        + $"(offending value: '{s}')");
                break;
        }
    }
}
