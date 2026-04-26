namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Optional free-text augmentation supplied by the runner when regenerating
/// their plan from Settings (Slice 1 § Unit 5 R05.4). The text is appended at
/// the END of the plan-generation user message under a stable label so the
/// macro/meso/micro chain prefix remains byte-stable across calls 2-6 and the
/// intent itself sits inside the cacheable prefix (DEC-047 / R-067).
/// </summary>
/// <param name="FreeText">
/// Sanitized runner-supplied free-text describing the regeneration intent
/// (e.g. "I'm coming back from a calf strain, drop volume by 20%").
/// <para>
/// The caller (Slice 1 § Unit 5 / T05.1) is responsible for invoking
/// <c>IPromptSanitizer.SanitizeAsync(intent.FreeText, PromptSection.RegenerationIntentFreeText, ct)</c>
/// BEFORE constructing this record. <see cref="ContextAssembler.ComposeForPlanGenerationAsync"/>
/// does NOT re-sanitize.
/// </para>
/// <para>
/// Capped at <see cref="MaxFreeTextLength"/> characters at construction. The
/// cap is sized to admit the post-sanitization payload: the wire-level raw
/// input is capped at <see cref="RawMaxFreeTextLength"/> by the controller,
/// and the layered sanitizer wraps the content with a Spotlighting delimiter
/// (e.g. <c>&lt;REGENERATION_INTENT id="..."&gt;…&lt;/REGENERATION_INTENT&gt;</c>)
/// that adds up to <see cref="DelimiterOverhead"/> additional characters.
/// </para>
/// </param>
public sealed record RegenerationIntent
{
    /// <summary>
    /// Maximum allowed length of the raw, pre-sanitization free-text supplied on
    /// the wire. Per Slice 1 spec § Unit 5 R05.1 this is the wire contract: the
    /// controller validates the raw request body against this cap and rejects
    /// over-length input with HTTP 400 BEFORE invoking the sanitizer.
    /// </summary>
    public const int RawMaxFreeTextLength = 500;

    /// <summary>
    /// Upper bound on the per-call delimiter overhead the layered sanitizer
    /// appends when wrapping the runner's free-text in a Spotlighting block.
    /// Sized to admit the longest delimiter label + a 16-hex-character per-turn
    /// nonce + the closing tag with comfortable headroom for future labels.
    /// </summary>
    public const int DelimiterOverhead = 200;

    /// <summary>
    /// Maximum allowed length of <see cref="FreeText"/> in UTF-16 code units.
    /// Equal to <see cref="RawMaxFreeTextLength"/> + <see cref="DelimiterOverhead"/>
    /// so a sanitizer-wrapped at-cap raw input round-trips through this record
    /// without spurious construction failures.
    /// </summary>
    public const int MaxFreeTextLength = RawMaxFreeTextLength + DelimiterOverhead;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegenerationIntent"/> class.
    /// Initializes a new instance of the <see cref="RegenerationIntent"/> record.
    /// </summary>
    /// <param name="freeText">Sanitized free-text intent. Must not exceed <see cref="MaxFreeTextLength"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="freeText"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="freeText"/> exceeds the length cap.</exception>
    public RegenerationIntent(string freeText)
    {
        ArgumentNullException.ThrowIfNull(freeText);
        if (freeText.Length > MaxFreeTextLength)
        {
            throw new ArgumentException(
                $"RegenerationIntent.FreeText length {freeText.Length} exceeds the {MaxFreeTextLength}-character cap.",
                nameof(freeText));
        }

        FreeText = freeText;
    }

    /// <summary>Gets the sanitized free-text intent.</summary>
    public string FreeText { get; }
}
