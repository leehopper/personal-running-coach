using System.Text.RegularExpressions;

namespace RunCoach.Api.Modules.Coaching;

/// <summary>
/// Deterministic boundary guard that scrubs the trademarked four-letter
/// pace-index term from LLM-authored prose (Slice 3B F2). Prompt-level
/// vocabulary rules make an occurrence rare; this scrub is the runtime
/// backstop that guarantees the term never reaches a persisted event or an
/// API-visible field, regardless of which structured-output type carried it.
/// </summary>
/// <remarks>
/// The pattern spells the term as <c>V[.-]?DOT</c> (optional separator,
/// optional plural, word-bounded, case-insensitive) so the source never
/// contains the contiguous mark itself. Both the replacement and the term
/// are free of JSON-significant characters, so scrubbing raw structured-output
/// JSON before deserialization cannot break parsing.
/// </remarks>
internal static partial class TrademarkScrubber
{
    /// <summary>
    /// The approved replacement vocabulary substituted for each occurrence.
    /// </summary>
    internal const string Replacement = "pace-zone index";

    /// <summary>
    /// Returns a value indicating whether <paramref name="text"/> contains the
    /// trademarked term (case-insensitive, optional dot/hyphen separator).
    /// </summary>
    internal static bool ContainsTrademarkedTerm(string text)
    {
        return TrademarkedTerm().IsMatch(text);
    }

    /// <summary>
    /// Replaces every occurrence of the trademarked term in
    /// <paramref name="text"/> with <see cref="Replacement"/>. Returns the
    /// original instance when the text is clean, so the hot no-leak path
    /// allocates nothing.
    /// </summary>
    internal static string Scrub(string text, out int occurrences)
    {
        occurrences = TrademarkedTerm().Count(text);
        return occurrences == 0 ? text : TrademarkedTerm().Replace(text, Replacement);
    }

    [GeneratedRegex(@"\bV[.-]?DOTs?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrademarkedTerm();
}
