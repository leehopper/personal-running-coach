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
/// Capped at <see cref="MaxFreeTextLength"/> characters at construction; the
/// 500-char cap is a placeholder per the spec's "Open Considerations" — see
/// Slice 1 spec § Open Considerations for review criteria.
/// </para>
/// </param>
public sealed record RegenerationIntent
{
    /// <summary>Maximum allowed length of <see cref="FreeText"/> in UTF-16 code units.</summary>
    public const int MaxFreeTextLength = 500;

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
