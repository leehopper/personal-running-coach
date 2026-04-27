namespace RunCoach.Api.Modules.Coaching.Models;

/// <summary>
/// Optional free-text augmentation supplied by the runner when regenerating
/// their plan from Settings (Slice 1 § Unit 5 R05.4). The text is appended at
/// the END of the plan-generation user message under a stable label so the
/// macro/meso/micro chain prefix remains byte-stable across calls 2-6 and the
/// intent itself sits inside the cacheable prefix (DEC-047 / R-067).
/// </summary>
/// <param name="FreeText">
/// Raw runner-supplied free-text describing the regeneration intent
/// (e.g. "I'm coming back from a calf strain, drop volume by 20%"). The
/// controller validates the wire-level length cap and rejects anything
/// past <see cref="RawMaxFreeTextLength"/> with HTTP 400 before the value
/// reaches this record.
/// <para>
/// Sanitization is performed inside
/// <see cref="ContextAssembler.ComposeForPlanGenerationAsync"/> per DEC-059
/// — callers do NOT pre-sanitize. The assembler runs the layered Unicode
/// strip + 12-pattern regex catalog + Spotlighting delimiter wrap on the
/// <c>RegenerationIntentFreeText</c> section policy before interpolating
/// the value into the prompt.
/// </para>
/// <para>
/// Capped at <see cref="MaxFreeTextLength"/> characters at construction so
/// the in-memory record holds at most a sanitizer-wrapped value: the raw
/// wire input is capped at <see cref="RawMaxFreeTextLength"/> and the
/// layered sanitizer's Spotlighting delimiter
/// (e.g. <c>&lt;REGENERATION_INTENT id="..."&gt;…&lt;/REGENERATION_INTENT&gt;</c>)
/// adds up to <see cref="DelimiterOverhead"/> additional characters. The
/// generous ceiling means a callsite that defensively round-trips a
/// pre-wrapped value still constructs without spurious failures.
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
    /// <param name="freeText">Raw runner-supplied free-text intent. Must not exceed <see cref="MaxFreeTextLength"/>. The assembler sanitizes this value at prompt-build time.</param>
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

    /// <summary>Gets the raw free-text intent. Sanitization is applied inside <see cref="ContextAssembler.ComposeForPlanGenerationAsync"/>.</summary>
    public string FreeText { get; }
}
