using System;
using System.Diagnostics;
using FluentAssertions;
using RunCoach.Api.Modules.Coaching.Sanitization;

namespace RunCoach.Api.Tests.Modules.Coaching.Sanitization;

/// <summary>
/// Validates the Unicode-tier sanitization (UnicodeNormalizer.Strip) — the
/// always-neutralize first tier in <see cref="LayeredPromptSanitizer"/>.
/// Per Slice 1 § Unit 6 / R-068 § 5.1: must strip the U+E0000–U+E007F Tags
/// block and U+200B–U+200F + U+2060–U+2064 + U+FEFF zero-width / BOM ranges.
/// </summary>
public class UnicodeNormalizerTests
{
    [Fact]
    public void Strip_NullInput_ReturnsEmptyAndZero()
    {
        // Arrange
        string? input = null;

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert
        normalized.Should().BeEmpty();
        stripped.Should().Be(0);
    }

    [Fact]
    public void Strip_PlainAscii_LeavesUntouched()
    {
        // Arrange
        var input = "I ran 10 miles today and felt great.";

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert
        normalized.Should().Be(input);
        stripped.Should().Be(0);
    }

    [Theory]
    [InlineData(0x200B, "ZWSP")]
    [InlineData(0x200C, "ZWNJ")]
    [InlineData(0x200D, "ZWJ")]
    [InlineData(0x200E, "LRM")]
    [InlineData(0x200F, "RLM")]
    [InlineData(0x2060, "WJ")]
    [InlineData(0x2061, "function-application")]
    [InlineData(0x2062, "invisible-times")]
    [InlineData(0x2063, "invisible-separator")]
    [InlineData(0x2064, "invisible-plus")]
    [InlineData(0xFEFF, "BOM")]
    public void Strip_ZeroWidthCodepoint_RemovesIt(int codepoint, string label)
    {
        // Arrange — zero-width chars sandwiched in plain ASCII.
        var zw = char.ConvertFromUtf32(codepoint);
        var input = $"a{zw}b";

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert — label disambiguates the parameterized failure message.
        normalized.Should().Be("ab", $"the {label} (U+{codepoint:X4}) char must be stripped");
        stripped.Should().Be(1);
    }

    [Fact]
    public void Strip_UnicodeTagBlock_RemovesNonBmpPair()
    {
        // Arrange — U+E0049 ("LATIN CAPITAL LETTER I" tag) encodes as a
        // surrogate pair in UTF-16. Cisco / Trend Micro / AWS research
        // show this block is the canonical Unicode-tag-injection envelope.
        var tagI = char.ConvertFromUtf32(0xE0049);
        var tagG = char.ConvertFromUtf32(0xE0067);
        var tagN = char.ConvertFromUtf32(0xE006E);
        var input = $"hello{tagI}{tagG}{tagN}world";

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert — every tag-block char is two UTF-16 units; three chars × 2 = 6.
        normalized.Should().Be("helloworld");
        stripped.Should().Be(6);
    }

    [Fact]
    public void Strip_TagBlockBoundary_E0000AndE007F_RemovesBoth()
    {
        // Arrange
        var first = char.ConvertFromUtf32(0xE0000);
        var last = char.ConvertFromUtf32(0xE007F);
        var input = $"x{first}y{last}z";

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert
        normalized.Should().Be("xyz");
        stripped.Should().Be(4); // two surrogate pairs
    }

    [Fact]
    public void Strip_NonBmpOutsideTagBlock_LeavesUntouched()
    {
        // Arrange — a regular non-BMP emoji (U+1F600) must pass through.
        var emoji = char.ConvertFromUtf32(0x1F600);
        var input = $"good run {emoji}";

        // Act
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);

        // Assert
        normalized.Should().Be(input);
        stripped.Should().Be(0);
    }

    [Fact]
    public void Strip_RegexRunsWithinTimeoutBudget()
    {
        // Arrange — synthetic large input: zero-width chars sprinkled in plain
        // text. The 50 ms ReDoS guard must not fire on real content.
        var zwsp = char.ConvertFromUtf32(0x200B);
        var input = new string('a', 50_000) + zwsp + new string('b', 50_000);

        // Act
        var sw = Stopwatch.StartNew();
        var (normalized, stripped) = UnicodeNormalizer.Strip(input);
        sw.Stop();

        // Assert — operation completes well under the 50 ms regex timeout.
        normalized.Length.Should().Be(input.Length - 1);
        stripped.Should().Be(1);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void RegexTimeout_Is50Milliseconds()
    {
        // Arrange / Act
        var actualTimeout = UnicodeNormalizer.RegexTimeout;

        // Assert
        actualTimeout.Should().Be(
            TimeSpan.FromMilliseconds(50),
            because: "R-068 § 5.1 / 5.2 requires a 50 ms ReDoS guard");
    }
}
