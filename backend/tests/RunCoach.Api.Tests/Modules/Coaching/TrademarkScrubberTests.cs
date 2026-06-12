using System.Text.Json.Nodes;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Live trademark-guard tests for <see cref="TrademarkScrubber"/> (Slice 3B F2):
/// the deterministic boundary scrub that removes the trademarked term "VDOT"
/// from LLM-authored prose before it is deserialized, persisted, or returned.
/// This test file is carve-out-exempt from the repo-wide trademark scan — like
/// <c>ContextAssemblerTests</c> and <c>OnboardingPromptTests</c>, it must spell
/// the term out to prove it gets scrubbed.
/// </summary>
public class TrademarkScrubberTests
{
    [Fact]
    public void Scrub_ReplacesTheTerm_InTheRecordedLiveLeakShape()
    {
        // Arrange — the exact prose shape the 2026-06-11 live pass persisted
        // into Macro.Rationale (Slice 3B F2).
        var leaked = "Using Daniels' Running Formula, your VDOT sits around 38";

        // Act
        var actual = TrademarkScrubber.Scrub(leaked, out var occurrences);

        // Assert
        actual.Should().Be("Using Daniels' Running Formula, your pace-zone index sits around 38");
        occurrences.Should().Be(1);
    }

    [Theory]
    [InlineData("Your VDOT is 38.", "Your pace-zone index is 38.")]
    [InlineData("Your vdot is 38.", "Your pace-zone index is 38.")]
    [InlineData("Your Vdot is 38.", "Your pace-zone index is 38.")]
    [InlineData("Your V.DOT is 38.", "Your pace-zone index is 38.")]
    [InlineData("Your V-DOT is 38.", "Your pace-zone index is 38.")]
    [InlineData("Your VDOTs improved.", "Your pace-zone index improved.")]
    public void Scrub_ReplacesCaseAndSeparatorVariants(string input, string expected)
    {
        // Act
        var actual = TrademarkScrubber.Scrub(input, out var occurrences);

        // Assert
        actual.Should().Be(expected);
        occurrences.Should().Be(1);
    }

    [Fact]
    public void Scrub_CountsEveryOccurrence()
    {
        // Arrange
        var input = "VDOT here, vdot there, and V.DOT everywhere.";

        // Act
        var actual = TrademarkScrubber.Scrub(input, out var occurrences);

        // Assert
        occurrences.Should().Be(3);
        actual.Should().Be(
            "pace-zone index here, pace-zone index there, and pace-zone index everywhere.");
    }

    [Theory]
    [InlineData("An anecdote about pacing.")]
    [InlineData("The AVDOT acronym is unrelated.")]
    [InlineData("VDOTING is not a word the model writes.")]
    public void Scrub_LeavesNonMatchingWords_Untouched(string input)
    {
        // Act
        var actual = TrademarkScrubber.Scrub(input, out var occurrences);

        // Assert
        occurrences.Should().Be(0);
        actual.Should().Be(input);
    }

    [Fact]
    public void Scrub_ReturnsTheSameInstance_WhenTextIsClean()
    {
        // Arrange — the no-leak path is every call in practice; it must not allocate.
        var clean = "Train in your easy pace-zone this week.";

        // Act
        var actual = TrademarkScrubber.Scrub(clean, out var occurrences);

        // Assert
        occurrences.Should().Be(0);
        actual.Should().BeSameAs(clean);
    }

    [Fact]
    public void Scrub_PreservesJsonValidity_WhenScrubbingInsideStringValues()
    {
        // Arrange — the production call site scrubs the raw structured-output
        // JSON before deserialization; the replacement must never break parsing.
        var json = """{"rationale":"Using Daniels' Running Formula, your VDOT sits around 38","total_weeks":16}""";

        // Act
        var actual = TrademarkScrubber.Scrub(json, out _);

        // Assert
        var parsed = System.Text.Json.JsonDocument.Parse(actual);
        parsed.RootElement.GetProperty("rationale").GetString()
            .Should().Be("Using Daniels' Running Formula, your pace-zone index sits around 38");
        parsed.RootElement.GetProperty("total_weeks").GetInt32().Should().Be(16);
    }

    [Theory]
    [InlineData("Your VDOT is 38.", true)]
    [InlineData("Your v-dot is 38.", true)]
    [InlineData("Train in your easy pace-zone this week.", false)]
    [InlineData("An anecdote about pacing.", false)]
    public void ContainsTrademarkedTerm_DetectsTheTermCaseInsensitively(string input, bool expected)
    {
        // Act
        var actual = TrademarkScrubber.ContainsTrademarkedTerm(input);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void ScrubJsonStringValues_ReplacesTheTerm_AfterJsonNewlineEscape()
    {
        // Arrange — raw JSON where the term immediately follows a \n escape sequence in a string
        // value. The plain Scrub() misses this because the regex \b sees 'n' (a word char) before
        // 'V' and the boundary does not fire. ScrubJsonStringValues() operates on decoded string
        // values so the actual newline character precedes 'V' and the boundary fires correctly.
        var json = """{"rationale":"Key metrics:\nVDOT: 38","total_weeks":16}""";
        var node = JsonNode.Parse(json)!;

        // Act
        var occurrences = TrademarkScrubber.ScrubJsonStringValues(node);

        // Assert
        occurrences.Should().Be(1);
        node["rationale"]!.GetValue<string>()
            .Should().Be("Key metrics:\npace-zone index: 38");
    }
}
