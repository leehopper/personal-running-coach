using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Shared eval-side trademark guard (Slice 3B F2): serializes an LLM output
/// (or an anonymous bundle of outputs) and asserts that no prose field
/// contains the trademarked pace-index term. Single-sources the detection
/// pattern from <see cref="TrademarkScrubber"/> so this guard and the
/// production boundary scrub cannot drift apart. A failure here means a newly
/// recorded fixture regressed against the generation prompt's vocabulary rule
/// — tighten the prompt and re-record; the production scrub remains the
/// runtime backstop either way.
/// </summary>
internal static class TrademarkProseGuard
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Asserts that every string field reachable from <paramref name="output"/>
    /// is free of the trademarked pace-index term (case-insensitive, separator
    /// and plural variants included).
    /// </summary>
    internal static void AssertClean(string label, object output)
    {
        var json = JsonSerializer.Serialize(output, SerializeOptions);
        TrademarkScrubber.ContainsTrademarkedTerm(json).Should().BeFalse(
            because: $"every persisted prose field of '{label}' must use 'Daniels-Gilbert zones' / "
                + "'pace-zone index' and never the trademarked pace-index term (Slice 3B F2)");
    }
}
