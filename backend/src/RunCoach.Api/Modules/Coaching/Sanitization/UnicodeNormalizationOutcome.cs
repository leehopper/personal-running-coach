namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Result of <see cref="UnicodeNormalizer.Strip(string?)"/>. Carries the
/// stripped text plus per-category counts so the layered sanitizer can emit
/// independent <see cref="SanitizationCategory.UnicodeTag"/> and
/// <see cref="SanitizationCategory.ZeroWidth"/> findings without losing the
/// dimension when both classes are present in the same input.
/// </summary>
/// <param name="Normalized">Input text with tag-block and zero-width / BOM
/// chars removed.</param>
/// <param name="TagBlockChars">Count of UTF-16 code units removed from the
/// U+E0000–U+E007F Unicode Tags block. Each tag-block code point is a non-BMP
/// surrogate pair, so this value is always even.</param>
/// <param name="ZeroWidthChars">Count of UTF-16 code units removed from the
/// zero-width and BOM ranges (U+200B–U+200F, U+2060–U+2064, U+FEFF).</param>
internal readonly record struct UnicodeNormalizationOutcome(
    string Normalized,
    int TagBlockChars,
    int ZeroWidthChars)
{
    /// <summary>Gets total UTF-16 code units stripped across both categories.</summary>
    public int TotalStripped => TagBlockChars + ZeroWidthChars;
}
