using System.Threading;
using System.Threading.Tasks;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Best-effort defense-in-depth sanitization of user-controlled free-text
/// before it reaches assembled LLM prompt sections. Implements the layered
/// containment-first mitigation per Slice 1 § Unit 6 / DEC-059 / R-068.
/// </summary>
/// <remarks>
/// <para>
/// <b>Design contract:</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Deterministic:</b> two calls with the same <c>(input, section)</c>
///       produce byte-equal sanitized output, with the lone exception of the
///       per-turn <c>id="..."</c> nonce. The nonce is appended on the
///       non-cached tail (current user message) so cache-prefix stability is
///       preserved per DEC-047.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Side-effect-free w.r.t. caller:</b> audit logging is internal; the
///       method does not throw on suspicious content. The caller decides policy.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Section-aware:</b> different <see cref="PromptSection"/> values
///       receive different policies (Unicode-strip + regex tier + delimiter)
///       per the policy table in Slice 1 § Unit 6.
///     </description>
///   </item>
/// </list>
/// <para>
/// Anthropic's Constitutional AI training, the typed <c>system</c> parameter,
/// and constrained decoding remain the primary mitigations. This sanitizer's
/// unique job is (1) Unicode-tag and zero-width stripping, (2) deterministic
/// auditable handling of high-frequency direct-injection patterns, and
/// (3) Spotlighting containment delimiters at section boundaries.
/// </para>
/// </remarks>
public interface IPromptSanitizer
{
    /// <summary>
    /// Sanitizes a single user-controlled string for the given prompt section.
    /// </summary>
    /// <param name="input">User-controlled free-text. <c>null</c> and empty
    /// values are treated as empty content; the wrapped delimiter is still
    /// emitted around an empty body.</param>
    /// <param name="section">The destination prompt section, which selects the
    /// per-section policy (regex tier, neutralize/log-only, delimiter label).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sanitized payload + finding list.</returns>
    ValueTask<SanitizationResult> SanitizeAsync(
        string? input,
        PromptSection section,
        CancellationToken ct = default);
}
