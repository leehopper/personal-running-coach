using System.Text;
using FluentAssertions;
using RunCoach.Api.Modules.Observability.Controllers;

namespace RunCoach.Api.Tests.Modules.Observability;

/// <summary>
/// Unit coverage for <see cref="ClientErrorsController.TruncateStack(string)"/>.
/// The integration test only exercises the long-multibyte-stack path through a
/// real Postgres round-trip; these focused unit cases cover the small-stack
/// early-return and the boundary case where the stack is exactly the cap.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ClientErrorsControllerTruncateStackTests
{
    [Fact]
    public void TruncateStack_ReturnsAsIs_WhenStackIsWellUnderCap()
    {
        const string expected = "at Foo (bar.js:12:34)\nat Baz (qux.ts:55:6)";

        var actual = ClientErrorsController.TruncateStack(expected);

        actual.Should().Be(expected);
    }

    [Fact]
    public void TruncateStack_ReturnsAsIs_WhenStackIsExactlyAtCap()
    {
        var atCap = new string('a', ClientErrorsController.MaxStackBytes);

        var actual = ClientErrorsController.TruncateStack(atCap);

        actual.Should().Be(atCap);
        Encoding.UTF8.GetByteCount(actual)
            .Should().Be(ClientErrorsController.MaxStackBytes);
    }

    [Fact]
    public void TruncateStack_TrimsAsciiPayload_AppendingSuffix()
    {
        var oversize = new string('a', ClientErrorsController.MaxStackBytes + 4096);

        var actual = ClientErrorsController.TruncateStack(oversize);

        actual.Should().EndWith(ClientErrorsController.StackTruncationSuffix);
        Encoding.UTF8.GetByteCount(actual)
            .Should().BeLessThanOrEqualTo(ClientErrorsController.MaxStackBytes);
    }

    [Fact]
    public void TruncateStack_TrimsMultibytePayload_StayingUnderByteCap()
    {
        // 8 000 CJK chars × 3 UTF-8 bytes = 24 000 bytes — clearly over the 16 KiB cap.
        var oversize = new string('漢', 8000);

        var actual = ClientErrorsController.TruncateStack(oversize);

        actual.Should().EndWith(ClientErrorsController.StackTruncationSuffix);
        Encoding.UTF8.GetByteCount(actual)
            .Should().BeLessThanOrEqualTo(ClientErrorsController.MaxStackBytes);
    }

    [Fact]
    public void TruncateStack_PreservesRuneBoundaries_NeverEmittingPartialMultibyteChar()
    {
        // Emoji (4-byte UTF-8 supplementary-plane code point). Verifies the
        // rune-walk doesn't cut a code point in half: every rune in the
        // result decodes cleanly, so the byte-budget check holds.
        var oversizeEmoji = string.Concat(Enumerable.Repeat("😀", 5000));

        var actual = ClientErrorsController.TruncateStack(oversizeEmoji);

        var rebuilt = new string(actual.EnumerateRunes()
            .SelectMany(r => r.ToString().ToCharArray()).ToArray());
        actual.Should().Be(rebuilt);
        Encoding.UTF8.GetByteCount(actual)
            .Should().BeLessThanOrEqualTo(ClientErrorsController.MaxStackBytes);
    }
}
