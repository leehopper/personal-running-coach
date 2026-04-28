using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// The 12-pattern regex catalog (PI-01..PI-12) sourced from the OWASP LLM
/// Prompt Injection Prevention Cheat Sheet, the Lakera Gandalf corpus,
/// ProtectAI Rebuff <c>injectionKeywords</c>, and the Shen et al. CCS '24
/// jailbreak corpus. Spec: Slice 1 § Unit 6 / DEC-059 / R-068 § 5.2.
/// </summary>
/// <remarks>
/// <para>
/// Patterns that contain a whitespace token between mandatory components use
/// <c>\s+</c> or <c>\s*</c> (depending on whether emptiness between tokens is
/// allowed) — never a literal space — to defeat space-injection bypasses.
/// PI-12 (base64 advisory) has no whitespace token because valid base64 is
/// inherently space-free. Patterns compile with
/// <see cref="RegexOptions.CultureInvariant"/> |
/// <see cref="RegexOptions.Compiled"/> plus a 50 ms
/// <see cref="Regex.MatchTimeout"/> ReDoS guard;
/// <see cref="RegexOptions.IgnoreCase"/> is added for every pattern except
/// PI-12, where the case-sensitive base64 alphabet matters.
/// </para>
/// <para>
/// The catalog deliberately omits a standalone <c>\bignore\b</c> pattern —
/// that was empirically the highest-false-positive trigger in the validation
/// corpus ("I want to ignore my last race"). PI-01 requires "ignore … (previous|...) instructions"
/// to fire. Leet-speak and homoglyph variants are deferred to MVP-1+ per R-068 § 5.2.
/// </para>
/// </remarks>
internal sealed class PatternCatalog
{
    /// <summary>
    /// Catalog version stamped onto OTel attributes and structured logs.
    /// Bump on any pattern change so audit replay can pin the active rule
    /// set.
    /// </summary>
    public const string PolicyVersion = "v1.1.0";

    private PatternCatalog(ImmutableArray<CatalogPattern> patterns)
    {
        Patterns = patterns;
    }

    /// <summary>
    /// Gets singleton instance — patterns are stateless and compiled once at
    /// type-load.
    /// </summary>
    public static PatternCatalog Default { get; } = new(BuildDefaultPatterns());

    /// <summary>
    /// Gets identifiers of the patterns promoted to neutralize-mode for
    /// <see cref="PromptSection.CurrentUserMessage"/> at MVP-0. Per Slice 1
    /// § Unit 6: DAN-family triggers (PI-04, PI-05, PI-06) carry near-zero
    /// false-positive risk in a running context and so are stripped on hit
    /// for the current user message only.
    /// </summary>
    public static IReadOnlySet<string> NeutralizeOnCurrentUserMessage { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "PI-04", "PI-05", "PI-06" };

    /// <summary>
    /// Gets identifiers of the patterns evaluated for the "light" tier sections
    /// (short proper nouns: <see cref="PromptSection.UserProfileName"/>,
    /// <see cref="PromptSection.GoalStateRaceName"/>). Only role-spoof
    /// tokens (PI-07) are checked.
    /// </summary>
    public static IReadOnlySet<string> LightTierPatterns { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "PI-07" };

    /// <summary>Gets the compiled patterns. Order is the canonical PI-01..PI-12 order.</summary>
    public ImmutableArray<CatalogPattern> Patterns { get; }

    /// <summary>
    /// Test-only factory: build a catalog backed by an arbitrary pattern set.
    /// Intended for regression tests that exercise edge behavior (e.g. ReDoS
    /// timeout handling) without polluting the production catalog. Visible
    /// to the test assembly via <c>InternalsVisibleTo</c>.
    /// </summary>
    internal static PatternCatalog ForTesting(ImmutableArray<CatalogPattern> patterns) =>
        new(patterns);

    private static Regex Compile(string pattern, bool ignoreCase = true)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        return new Regex(pattern, options, UnicodeNormalizer.RegexTimeout);
    }

    private static ImmutableArray<CatalogPattern> BuildDefaultPatterns() =>
        ImmutableArray.Create(
            new CatalogPattern(
                "PI-01",
                SanitizationCategory.RegexHitDirectOverride,
                Compile(@"\b(?:ignore|forget|disregard)\s+(?:(?:(?:all|any|the|your|previous|prior|preceding|earlier)\s+)*(?:above|preceding|previous)\b|(?:all\s+|any\s+|the\s+|your\s+|previous\s+|prior\s+|preceding\s+|earlier\s+|above\s+)+(?:instructions?|rules?|prompts?|directives?)\b)")),
            new CatalogPattern(
                "PI-02",
                SanitizationCategory.RegexHitDirectOverride,
                Compile(@"\bdisregard\s+(?:all\s+|the\s+|your\s+|previous\s+|prior\s+|earlier\s+|above\s+)+(?:instructions?|rules?|prompts?)\b")),
            new CatalogPattern(
                "PI-03",
                SanitizationCategory.RegexHitForget,
                Compile(@"\bforget\s+(?:everything|all|your\s+(?:previous|prior)\s+(?:instructions|training))\b")),
            new CatalogPattern(
                "PI-04",
                SanitizationCategory.RegexHitPersonaInjection,
                Compile(@"\b(?:you\s+are\s+now|act\s+as|pretend\s+to\s+be|from\s+now\s+on\s+you\s+are)\b[^.!?\n]{0,40}?\b(?:DAN|STAN|AIM|DUDE|developer\s+mode|jailbroken|unrestricted)\b")),
            new CatalogPattern(
                "PI-05",
                SanitizationCategory.RegexHitPersonaInjection,
                Compile(@"\bdo\s+anything\s+now\b")),
            new CatalogPattern(
                "PI-06",
                SanitizationCategory.RegexHitPersonaInjection,
                Compile(@"\b(?:(?:in|with|to|enter(?:ing)?|enable(?:d|ing)?|activate(?:d|ing)?)\s+developer\s+mode|developer\s+mode\s+(?:enabled|on|activated|engaged))\b")),
            new CatalogPattern(
                "PI-07",
                SanitizationCategory.RegexHitRoleSpoof,
                Compile(@"\[(?:SYSTEM|INST|ASSISTANT)\]\s*[:.]|<\s*(?:system|inst|assistant)\s*>")),
            new CatalogPattern(
                "PI-08",
                SanitizationCategory.RegexHitSystemPromptLeak,
                Compile(@"\b(?:reveal|show|print(?:\s+out)?|output|repeat|echo|tell|give|recite|display|leak)\s+(?:me\s+|us\s+)?(?:(?:your|the|all\s+(?:your|the|previous|prior))\s+)?(?:(?:contents?|text)s?\s+of\s+(?:your|the)\s+)?(?:(?:initial|original|prior|previous)\s+)?(?:system\s+prompt|instructions?|initial\s+message|initial\s+prompt|prompt\s+verbatim|message|prompt)\b")),
            new CatalogPattern(
                "PI-09",
                SanitizationCategory.RegexHitDirectOverride,
                Compile(@"\b(?:above\s+(?:is\s+)?wrong|everything\s+above\s+(?:is|was)\s+wrong)\b")),
            new CatalogPattern(
                "PI-10",
                SanitizationCategory.RegexHitDirectOverride,
                Compile(@"\bsystem\s+(?:override|prompt\s+override|jailbreak)\b")),
            new CatalogPattern(
                "PI-11",
                SanitizationCategory.RegexHitDirectOverride,
                Compile(@"\bnew\s+instructions?\b(?:\s+\w+){0,5}\s*[:.]")),
            new CatalogPattern(
                "PI-12",
                SanitizationCategory.RegexHitBase64Advisory,
                Compile(@"[A-Za-z0-9+/]{60,}={0,2}", ignoreCase: false)));

    /// <summary>
    /// Single compiled pattern in the catalog. Carries its stable id (e.g.
    /// "PI-01") and category for telemetry, plus the precompiled regex.
    /// </summary>
    public sealed record CatalogPattern(
        string PatternId,
        SanitizationCategory Category,
        Regex Regex);
}
