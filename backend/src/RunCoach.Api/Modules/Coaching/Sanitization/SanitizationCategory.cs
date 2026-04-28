namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// High-level classification of a sanitization finding. Spans the deterministic
/// Unicode normalization tier and the regex pattern catalog tier per
/// Slice 1 § Unit 6 / R-068. Used in OTel attributes and structured logs as
/// the bucketed signal; the per-finding <c>PatternId</c> carries the granular id.
/// </summary>
public enum SanitizationCategory
{
    /// <summary>Stripped Unicode tag-block code points (U+E0000–U+E007F).</summary>
    UnicodeTag,

    /// <summary>Stripped zero-width or BOM code points (U+200B–U+200F, U+2060–U+2064, U+FEFF).</summary>
    ZeroWidth,

    /// <summary>
    /// Direct override / instruction-overwrite pattern matched (PI-01, PI-02,
    /// PI-09, PI-10, PI-11).
    /// </summary>
    RegexHitDirectOverride,

    /// <summary>Forget / instruction-erasure pattern matched (PI-03).</summary>
    RegexHitForget,

    /// <summary>
    /// Persona / jailbreak persona pattern matched (PI-04, PI-05, PI-06 — DAN family).
    /// </summary>
    RegexHitPersonaInjection,

    /// <summary>Role-spoof / system-tag pattern matched (PI-07).</summary>
    RegexHitRoleSpoof,

    /// <summary>System-prompt-leak request pattern matched (PI-08).</summary>
    RegexHitSystemPromptLeak,

    /// <summary>Base64-shaped advisory pattern matched (PI-12).</summary>
    RegexHitBase64Advisory,

    /// <summary>
    /// Regex evaluation hit the 50 ms <c>RegexMatchTimeoutException</c> guard.
    /// Recorded with <see cref="SanitizationFinding.Stripped"/> = false so the audit
    /// trail captures the suspicious-input signal without breaking the
    /// <see cref="IPromptSanitizer"/> no-throw contract.
    /// </summary>
    RegexTimeout,
}
