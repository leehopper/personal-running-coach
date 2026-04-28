using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RunCoach.Api.Modules.Coaching.Sanitization;

/// <summary>
/// Three-tier layered prompt-injection sanitizer per Slice 1 § Unit 6 / DEC-059
/// / R-068:
/// <list type="number">
///   <item><description>Deterministic Unicode normalization (always neutralize).</description></item>
///   <item><description>12-pattern regex catalog (log-only at MVP-0; DAN-family stripped on <see cref="PromptSection.CurrentUserMessage"/>).</description></item>
///   <item><description>Spotlighting-style containment delimiters with per-turn nonce.</description></item>
/// </list>
/// </summary>
public sealed partial class LayeredPromptSanitizer : IPromptSanitizer
{
    /// <summary>Name of the OTel ActivitySource for sanitization spans.</summary>
    internal const string ActivitySourceName = "RunCoach.Llm";

    /// <summary>Name of the child span emitted per <see cref="SanitizeAsync"/> call.</summary>
    internal const string SanitizationSpanName = "runcoach.llm.sanitization";

    private static readonly ActivitySource Source = new(ActivitySourceName);

    private readonly ILogger<LayeredPromptSanitizer> _logger;
    private readonly PatternCatalog _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LayeredPromptSanitizer"/> class.
    /// Initializes a new instance using the default pattern catalog.
    /// </summary>
    public LayeredPromptSanitizer(ILogger<LayeredPromptSanitizer> logger)
        : this(logger, PatternCatalog.Default)
    {
    }

    internal LayeredPromptSanitizer(
        ILogger<LayeredPromptSanitizer> logger,
        PatternCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(catalog);
        _logger = logger;
        _catalog = catalog;
    }

    /// <inheritdoc />
    public ValueTask<SanitizationResult> SanitizeAsync(
        string? input,
        PromptSection section,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        using var activity = Source.StartActivity(SanitizationSpanName, ActivityKind.Internal);

        var rawInput = input ?? string.Empty;
        var originalLength = rawInput.Length;
        var findings = new List<SanitizationFinding>(capacity: 4);

        // Tier 1: Unicode normalization (always neutralize). Per-category
        // counts let us emit independent UnicodeTag and ZeroWidth findings
        // when both are present so the audit trail keeps the dimension.
        // The 50 ms ReDoS guard inside the strip regex can throw — catch and
        // record a RegexTimeout finding rather than violating the
        // IPromptSanitizer no-throw contract.
        string normalized;
        int tagBlockChars;
        int zeroWidthChars;
        try
        {
            var outcome = UnicodeNormalizer.Strip(rawInput);
            normalized = outcome.Normalized;
            tagBlockChars = outcome.TagBlockChars;
            zeroWidthChars = outcome.ZeroWidthChars;
        }
        catch (RegexMatchTimeoutException)
        {
            // Tier-1 timeout: fall back to passthrough; record the suspicious
            // input as a timeout finding. Tier 2 still runs against rawInput.
            normalized = rawInput;
            tagBlockChars = 0;
            zeroWidthChars = 0;
            RecordTimeoutFinding(findings, section, "U-TIMEOUT", originalLength);
        }

        if (tagBlockChars > 0)
        {
            findings.Add(new SanitizationFinding(
                Category: SanitizationCategory.UnicodeTag,
                PatternId: "U-TAGS",
                OriginalLength: originalLength,
                SanitizedLength: originalLength - tagBlockChars,
                Stripped: true));
        }

        if (zeroWidthChars > 0)
        {
            findings.Add(new SanitizationFinding(
                Category: SanitizationCategory.ZeroWidth,
                PatternId: "U-ZW",
                OriginalLength: originalLength,
                SanitizedLength: originalLength - zeroWidthChars,
                Stripped: true));
        }

        var strippedChars = tagBlockChars + zeroWidthChars;

        // Tier 2: regex catalog. Patterns considered depend on section policy.
        // Detection runs against the post-Tier-1 `normalized` snapshot so that
        // overlapping patterns (e.g. PI-04 "act as ... developer mode" and
        // PI-06 "developer mode enabled") both register findings even when an
        // earlier strip would otherwise erase the substring a later pattern
        // anchors on. Stripping still cascades on `working` so the final
        // sanitized text reflects the union of all neutralized ranges.
        var working = normalized;
        var anyNeutralized = strippedChars > 0;

        foreach (var pattern in _catalog.Patterns)
        {
            if (!IsPatternConsidered(pattern.PatternId, section))
            {
                continue;
            }

            bool matched;
            try
            {
                matched = pattern.Regex.IsMatch(normalized);
            }
            catch (RegexMatchTimeoutException)
            {
                // Per-pattern detection timeout. Skip strip; continue with
                // the next pattern so a single bad pattern can't poison the
                // catalog.
                RecordTimeoutFinding(findings, section, pattern.PatternId + "-TIMEOUT", working.Length);
                continue;
            }

            if (!matched)
            {
                continue;
            }

            var stripThisPattern = ShouldNeutralize(pattern.PatternId, section);
            var preLength = working.Length;
            var postLength = preLength;

            if (stripThisPattern)
            {
                try
                {
                    working = pattern.Regex.Replace(working, string.Empty);
                }
                catch (RegexMatchTimeoutException)
                {
                    // Strip Replace timed out — preserve `working` and record.
                    RecordTimeoutFinding(findings, section, pattern.PatternId + "-STRIP-TIMEOUT", preLength);
                    continue;
                }

                postLength = working.Length;
                anyNeutralized = true;
            }

            findings.Add(new SanitizationFinding(
                Category: pattern.Category,
                PatternId: pattern.PatternId,
                OriginalLength: preLength,
                SanitizedLength: postLength,
                Stripped: stripThisPattern));

            // Log structured finding (EventId 4001) per R-068 § 9.2. PII
            // discipline: input strings never appear in log values.
            LogPromptInjectionFinding(
                _logger,
                section,
                pattern.PatternId,
                stripThisPattern,
                preLength,
                postLength,
                PatternCatalog.PolicyVersion);
        }

        // Tier 3: containment delimiter wrap.
        var wrapped = WrapInDelimiter(section, working);

        // Annotate the activity (PII-free) per R-068 § 9.1.
        StampActivityAttributes(activity, section, findings, strippedChars, originalLength, working.Length);

        var result = new SanitizationResult(
            Sanitized: wrapped,
            Neutralized: anyNeutralized,
            Findings: findings);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Generates a 16-hex-char per-turn nonce. Uses
    /// <see cref="RandomNumberGenerator.GetHexString(int, bool)"/> for an
    /// explicit CSPRNG contract (64 bits of entropy). The value is
    /// intentionally non-deterministic — it is the only such element in the
    /// sanitizer output — and lives on the non-cached prompt tail so cache
    /// prefix stability is preserved per DEC-047.
    /// </summary>
    internal static string GenerateNonce() =>
        RandomNumberGenerator.GetHexString(16, lowercase: true);

    private static void StampActivityAttributes(
        Activity? activity,
        PromptSection section,
        List<SanitizationFinding> findings,
        int strippedChars,
        int originalLength,
        int sanitizedLength)
    {
        if (activity is null)
        {
            return;
        }

        var findingsJson = SerializeFindings(findings);
        activity.SetTag("openinference.span.kind", "GUARDRAIL");
        activity.SetTag("runcoach.sanitization.input_section_count", 1);
        activity.SetTag("runcoach.sanitization.findings_count", findings.Count);
        activity.SetTag("runcoach.sanitization.neutralized_count", CountStripped(findings));
        activity.SetTag("runcoach.sanitization.findings", findingsJson);
        activity.SetTag("runcoach.sanitization.unicode_strip_byte_count", strippedChars);
        activity.SetTag("runcoach.sanitization.original_length_total", originalLength);
        activity.SetTag("runcoach.sanitization.sanitized_length_total", sanitizedLength);
        activity.SetTag("runcoach.sanitization.policy_version", PatternCatalog.PolicyVersion);
        activity.SetTag("runcoach.sanitization.section", section.ToString());
    }

    private static void RecordTimeoutFinding(
        List<SanitizationFinding> findings,
        PromptSection section,
        string patternId,
        int referenceLength)
    {
        // Length fields equal — the timeout means we did not modify the input.
        // PII-discipline: only the patternId, section, and length are recorded.
        findings.Add(new SanitizationFinding(
            Category: SanitizationCategory.RegexTimeout,
            PatternId: patternId,
            OriginalLength: referenceLength,
            SanitizedLength: referenceLength,
            Stripped: false));
        _ = section; // Section is captured by the OTel span attribute set by the caller.
    }

    private static bool IsPatternConsidered(string patternId, PromptSection section)
    {
        // Light tier sections (short proper nouns) only run PI-07 (role tokens).
        if (section is PromptSection.UserProfileName or PromptSection.GoalStateRaceName)
        {
            return PatternCatalog.LightTierPatterns.Contains(patternId);
        }

        // All other sections run the full catalog.
        return true;
    }

    private static bool ShouldNeutralize(string patternId, PromptSection section)
    {
        // MVP-0 policy: log-only across the board, except DAN-family on the
        // current user message. Promotions are gated by R-068-T3 (second-user
        // trigger) per the cycle plan.
        if (section == PromptSection.CurrentUserMessage)
        {
            return PatternCatalog.NeutralizeOnCurrentUserMessage.Contains(patternId);
        }

        return false;
    }

    private static string DelimiterLabel(PromptSection section) => section switch
    {
        PromptSection.UserProfileName => "USER_NAME",
        PromptSection.UserProfileInjuryNote => "INJURY_NOTE",
        PromptSection.UserProfileRaceCondition => "RACE_CONDITIONS",
        PromptSection.UserProfileConstraints => "CONSTRAINTS",
        PromptSection.GoalStateRaceName => "RACE_NAME",
        PromptSection.TrainingHistoryWorkoutNote => "WORKOUT_NOTE",
        PromptSection.ConversationHistoryUserMessage => "HISTORICAL_TURN",
        PromptSection.CurrentUserMessage => "CURRENT_USER_INPUT",
        PromptSection.RegenerationIntentFreeText => "REGENERATION_INTENT",
        _ => "USER_DATA",
    };

    private static int CountStripped(List<SanitizationFinding> findings)
    {
        var count = 0;
        foreach (var finding in findings)
        {
            if (finding.Stripped)
            {
                count++;
            }
        }

        return count;
    }

    private static string SerializeFindings(List<SanitizationFinding> findings)
    {
        if (findings.Count == 0)
        {
            return "[]";
        }

        var summaries = new List<FindingSummary>(findings.Count);
        foreach (var f in findings)
        {
            summaries.Add(new FindingSummary(f.Category.ToString(), f.PatternId, f.Stripped));
        }

        return JsonSerializer.Serialize(summaries, FindingSerializerContext.Default.ListFindingSummary);
    }

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "PromptInjectionFinding section={Section} patternId={PatternId} stripped={Stripped} originalLength={OriginalLength} sanitizedLength={SanitizedLength} policyVersion={PolicyVersion}")]
    private static partial void LogPromptInjectionFinding(
        ILogger logger,
        PromptSection section,
        string patternId,
        bool stripped,
        int originalLength,
        int sanitizedLength,
        string policyVersion);

    private static string WrapInDelimiter(PromptSection section, string body)
    {
        var label = DelimiterLabel(section);

        // Escape user-controlled angle brackets / ampersands so a payload
        // containing a literal `</LABEL>` cannot terminate the section early
        // and inject text outside the intended boundary.
        var escapedBody = EscapeDelimiterBody(body);

        // Per R-068 § 5.3 / § 8: the per-turn nonce only applies to the
        // CurrentUserMessage section (and structurally, the regenerate intent
        // section, since both sit on the non-cached prompt tail). All other
        // sections use a stable label-only delimiter so the cache prefix
        // hashes identically across replays.
        var carriesNonce = section is PromptSection.CurrentUserMessage
            or PromptSection.RegenerationIntentFreeText;

        if (carriesNonce)
        {
            var nonce = GenerateNonce();
            return string.Create(
                CultureInfo.InvariantCulture,
                $"<{label} id=\"{nonce}\">{escapedBody}</{label}>");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"<{label}>{escapedBody}</{label}>");
    }

    /// <summary>
    /// Escapes characters that an LLM could interpret as a section terminator
    /// in the surrounding spotlighting delimiter (<c>&lt;LABEL&gt;…&lt;/LABEL&gt;</c>).
    /// Escapes ASCII <c>&amp;</c> / <c>&lt;</c> / <c>&gt;</c> AND a defense-in-depth
    /// list of bracket-homoglyph code points: fullwidth (U+FF1C/U+FF1E),
    /// mathematical (U+27E8/U+27E9), single guillemets (U+2039/U+203A) and
    /// CJK angle brackets (U+3008–U+300B). NFKC normalization would be
    /// broader but also folds e.g. ① → 1, so we use a surgical replace list.
    /// </summary>
    private static string EscapeDelimiterBody(string body) =>
        body
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("＜", "&lt;", StringComparison.Ordinal)
            .Replace("＞", "&gt;", StringComparison.Ordinal)
            .Replace("⟨", "&lt;", StringComparison.Ordinal)
            .Replace("⟩", "&gt;", StringComparison.Ordinal)
            .Replace("‹", "&lt;", StringComparison.Ordinal)
            .Replace("›", "&gt;", StringComparison.Ordinal)
            .Replace("〈", "&lt;", StringComparison.Ordinal)
            .Replace("〉", "&gt;", StringComparison.Ordinal)
            .Replace("《", "&lt;", StringComparison.Ordinal)
            .Replace("》", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// PII-free serializable shape for the OTel <c>findings</c> attribute and
    /// for unit-test introspection.
    /// </summary>
    internal sealed record FindingSummary(
        string Category,
        string PatternId,
        bool Stripped);
}
