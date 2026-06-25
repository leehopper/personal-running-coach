using FluentAssertions;
using RunCoach.Api.Modules.Coaching;

namespace RunCoach.Api.Tests.Modules.Coaching;

/// <summary>
/// Tests for <see cref="StreamingTrademarkScrubber"/> — the streaming-aware boundary guard that
/// keeps the trademarked term "VDOT" out of streamed coaching deltas even when its characters are
/// split across delta boundaries. This test file is carve-out-exempt from the repo-wide trademark
/// scan (like <c>TrademarkScrubberTests</c>): it must spell the term out to prove it is scrubbed.
/// </summary>
public class StreamingTrademarkScrubberTests
{
    [Fact]
    public void Push_CleanDeltas_PreserveNaturalChunkingWithNoHoldback()
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act — neither delta ends on a viable term prefix, so each is emitted whole.
        var first = scrubber.Push("Easy ");
        var second = scrubber.Push("does it.");
        var tail = scrubber.Flush();

        // Assert
        first.Should().Be("Easy ");
        second.Should().Be("does it.");
        tail.Should().BeEmpty();
        scrubber.Occurrences.Should().Be(0);
    }

    [Fact]
    public void Push_TermWithinASingleDelta_IsScrubbed()
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act
        var emitted = scrubber.Push("Your VDOT is 38. ") + scrubber.Flush();

        // Assert
        emitted.Should().Be("Your pace-zone index is 38. ");
        scrubber.Occurrences.Should().Be(1);
    }

    [Theory]
    [InlineData("Your V", "DOT is 38.")]
    [InlineData("Your VD", "OT is 38.")]
    [InlineData("Your VDO", "T is 38.")]
    [InlineData("Your V.", "DOT is 38.")]
    public void Push_TermSplitAcrossDeltas_IsScrubbed(string first, string second)
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act — the term straddles the delta boundary; the held-back tail keeps the partial buffered
        // until the rest arrives, then the whole term is scrubbed.
        var output = scrubber.Push(first) + scrubber.Push(second) + scrubber.Flush();

        // Assert
        output.Should().Be("Your pace-zone index is 38.");
        scrubber.Occurrences.Should().Be(1);
    }

    [Fact]
    public void Push_TermAsTheVeryLastChunk_IsScrubbedOnFlush()
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act — the term arrives at the end with no trailing text; only Flush can confirm the
        // trailing word boundary and emit the scrubbed replacement.
        var output = scrubber.Push("Your zone is V") + scrubber.Push("DOT") + scrubber.Flush();

        // Assert
        output.Should().Be("Your zone is pace-zone index");
        scrubber.Occurrences.Should().Be(1);
    }

    [Fact]
    public void Push_WordStartingWithVButNotTheTerm_IsNotHeldBack()
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act — "Very" ends in a non-viable prefix, so nothing is held back or scrubbed.
        var output = scrubber.Push("Run Very") + scrubber.Flush();

        // Assert
        output.Should().Be("Run Very");
        scrubber.Occurrences.Should().Be(0);
    }

    [Fact]
    public void Push_VPrecededByWordChar_DoesNotTriggerHoldback()
    {
        // Arrange — a leading word char means `\bV` can never match, so "...aV" is emitted, not held.
        var scrubber = new StreamingTrademarkScrubber();

        // Act
        var first = scrubber.Push("HRVaV");
        var rest = scrubber.Flush();

        // Assert
        first.Should().Be("HRVaV");
        rest.Should().BeEmpty();
        scrubber.Occurrences.Should().Be(0);
    }

    [Fact]
    public void Flush_OnEmptyBuffer_ReturnsEmpty()
    {
        // Arrange
        var scrubber = new StreamingTrademarkScrubber();

        // Act & Assert
        scrubber.Flush().Should().BeEmpty();
        scrubber.Push(null).Should().BeEmpty();
        scrubber.Push(string.Empty).Should().BeEmpty();
    }
}
