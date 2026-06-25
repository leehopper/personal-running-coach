using System.Text;
using System.Text.RegularExpressions;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Streaming-aware companion to <see cref="TrademarkScrubber"/> that guarantees the trademarked
/// pace-index term never reaches a consumer even when it is split across stream deltas. A naive
/// per-delta <see cref="TrademarkScrubber.Scrub(string, out int)"/> would miss a term whose
/// characters straddle a delta boundary (e.g. <c>"…V"</c> then <c>"DOT…"</c>), so this buffer
/// holds back only the trailing run that could still grow into a match, scrubs everything safe to
/// emit, and flushes the remainder when the stream ends.
/// </summary>
/// <remarks>
/// The held-back tail is computed precisely: it is the longest word-bounded suffix that is a viable
/// prefix of the term pattern (<c>V[.-]?DOTs?</c>). A delta that does not end on such a prefix —
/// the overwhelmingly common case — is emitted whole, so clean streams keep their natural delta
/// chunking and incur no added latency. Not thread-safe: drive it from a single enumeration.
/// </remarks>
internal sealed partial class StreamingTrademarkScrubber
{
    /// <summary>Longest scrubbable token is <c>V.DOTs</c> (6 chars); never hold back more.</summary>
    private const int MaxTermLength = 6;

    private readonly StringBuilder _pending = new();

    /// <summary>Gets the running count of trademarked occurrences scrubbed so far.</summary>
    public int Occurrences { get; private set; }

    /// <summary>
    /// Appends a delta and returns the scrubbed text that is now safe to emit, retaining any
    /// trailing run that could still complete into a match on a later delta. Returns
    /// <see cref="string.Empty"/> when nothing is safe to emit yet.
    /// </summary>
    public string Push(string? delta)
    {
        if (!string.IsNullOrEmpty(delta))
        {
            _pending.Append(delta);
        }

        var hold = ComputePotentialMatchTail();
        var emitLength = _pending.Length - hold;
        if (emitLength <= 0)
        {
            return string.Empty;
        }

        var emit = _pending.ToString(0, emitLength);
        _pending.Remove(0, emitLength);
        return ScrubAndCount(emit);
    }

    /// <summary>
    /// Scrubs and returns whatever remains buffered, clearing the buffer. Call once the stream has
    /// ended cleanly so the final partial cannot hide a complete term.
    /// </summary>
    public string Flush()
    {
        if (_pending.Length == 0)
        {
            return string.Empty;
        }

        var remainder = _pending.ToString();
        _pending.Clear();
        return ScrubAndCount(remainder);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    [GeneratedRegex(@"^[vV][.-]?[dD]?[oO]?[tT]?[sS]?$", RegexOptions.CultureInvariant)]
    private static partial Regex ViableTermPrefix();

    private string ScrubAndCount(string text)
    {
        var scrubbed = TrademarkScrubber.Scrub(text, out var hits);
        Occurrences += hits;
        return scrubbed;
    }

    /// <summary>
    /// Returns the number of trailing characters that form a word-bounded, viable prefix of the
    /// term pattern (and so must be held back), or 0 when the buffer cannot end mid-term.
    /// </summary>
    private int ComputePotentialMatchTail()
    {
        var n = _pending.Length;
        var max = Math.Min(MaxTermLength, n);
        for (var k = max; k >= 1; k--)
        {
            // The leading boundary must hold: the char before the candidate run must be absent or a
            // non-word char, else `\bV` could never match here and the run is safe to emit.
            var boundaryIndex = n - k - 1;
            if (boundaryIndex >= 0 && IsWordChar(_pending[boundaryIndex]))
            {
                continue;
            }

            if (ViableTermPrefix().IsMatch(_pending.ToString(n - k, k)))
            {
                return k;
            }
        }

        return 0;
    }
}
