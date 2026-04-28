using System;
using System.Text.RegularExpressions;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Deterministic Unicode-tier sanitization. Strips the Unicode Tags block
/// (U+E0000–U+E007F — invisible "tag" code points used for prompt smuggling
/// per the Cisco/AWS/Trend Micro Unicode-tag-injection research) and zero-width
/// / BOM characters (U+200B–U+200F, U+2060–U+2064, U+FEFF).
/// </summary>
/// <remarks>
/// <para>
/// Always neutralizes — never log-only — because these characters carry no
/// legitimate signal in user-typed running notes and the false-positive cost
/// is structurally zero (Trend Micro 2025; arXiv 2603.00164). Anthropic's
/// upstream layers do not deterministically catch these.
/// </para>
/// <para>
/// Single compiled regex with <see cref="RegexOptions.CultureInvariant"/> +
/// <see cref="RegexOptions.Compiled"/> and a 50 ms <c>MatchTimeout</c> ReDoS
/// guard. Zero-width + BOM are matched via <c>\uXXXX</c> escapes; the U+E0000–
/// U+E007F Tags block is non-BMP and is matched via the surrogate-pair range
/// <c>\uDB40[\uDC00-\uDC7F]</c>.
/// </para>
/// </remarks>
internal static partial class UnicodeNormalizer
{
    /// <summary>
    /// Maximum runtime for a single regex match before the engine throws
    /// <see cref="RegexMatchTimeoutException"/>. Catalog-wide ReDoS guard.
    /// </summary>
    internal static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Strips Unicode-tag and zero-width characters from <paramref name="input"/>,
    /// returning per-category strip counts so the audit layer can emit
    /// independent findings for each class even when both are present in the
    /// same input.
    /// </summary>
    /// <param name="input">Input string. <c>null</c> is treated as empty.</param>
    /// <returns>
    /// <see cref="UnicodeNormalizationOutcome"/> carrying the normalized text plus
    /// separate counts (in UTF-16 code units) for tag-block (U+E0000–U+E007F)
    /// and zero-width / BOM (U+200B–U+200F, U+2060–U+2064, U+FEFF) strips.
    /// </returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Thrown if the strip regex hits its 50 ms ReDoS guard. Callers in the
    /// sanitizer pipeline catch this and emit a <see cref="SanitizationCategory.RegexTimeout"/>
    /// finding rather than letting it escape the public API.
    /// </exception>
    public static UnicodeNormalizationOutcome Strip(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new UnicodeNormalizationOutcome(string.Empty, 0, 0);
        }

        var tagBlockChars = 0;
        var zeroWidthChars = 0;

        var normalized = StripRegex().Replace(input, m =>
        {
            // Tag-block matches are surrogate pairs (length 2, lead = U+DB40).
            // Zero-width / BOM matches are BMP single chars (length 1).
            if (m.Length == 2 && m.Value[0] == '\uDB40')
            {
                tagBlockChars += m.Length;
            }
            else
            {
                zeroWidthChars += m.Length;
            }

            return string.Empty;
        });

        return new UnicodeNormalizationOutcome(normalized, tagBlockChars, zeroWidthChars);
    }

    /// <summary>
    /// Compiled stripping regex. The character class covers zero-width + BOM
    /// using <c>\uXXXX</c> escapes (so source stays pure ASCII). The
    /// U+E0000–U+E007F Tags block is non-BMP and arrives as a surrogate pair
    /// — <c>\uDB40[\uDC00-\uDC7F]</c> matches that block exactly.
    /// </summary>
    [GeneratedRegex(
        "[\\u200B-\\u200F\\u2060-\\u2064\\uFEFF]|\\uDB40[\\uDC00-\\uDC7F]",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        matchTimeoutMilliseconds: 50)]
    private static partial Regex StripRegex();
}
