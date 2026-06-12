using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Shared eval-side trademark guard (Slice 3B F2): serializes an LLM output
/// (or an anonymous bundle of outputs) and asserts that no prose field
/// contains the trademarked pace-index term. Single-sources the detection
/// pattern from <see cref="TrademarkScrubber"/> so this guard and the
/// production boundary scrub cannot drift apart. Scans decoded string values
/// (via <see cref="JsonNode"/>) rather than the serialized JSON text so the
/// detection is correct even when the term follows a JSON escape sequence.
/// A failure here means a newly recorded fixture regressed against the
/// generation prompt's vocabulary rule — tighten the prompt and re-record;
/// the production scrub remains the runtime backstop either way.
/// </summary>
internal static class TrademarkProseGuard
{
    /// <summary>
    /// Asserts that every string field reachable from <paramref name="output"/>
    /// is free of the trademarked pace-index term (case-insensitive, separator
    /// and plural variants included).
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
                TrademarkScrubber.ContainsTrademarkedTerm(s).Should().BeFalse(
                    because: $"every persisted prose field of '{label}' must use 'Daniels-Gilbert zones' / "
                        + "'pace-zone index' and never the trademarked pace-index term (Slice 3B F2)");
                break;
        }
    }
}
