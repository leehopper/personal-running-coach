using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace RunCoach.Api.Modules.Training.Safety;

/// <summary>
/// The versioned high-risk keyword/threshold resource for the deterministic
/// <see cref="SafetyGate"/> (Slice 3 Unit 3 / DEC-079). Each <see cref="SafetyRule"/>
/// maps a compiled, word-boundary regex over a runner's free-text to a
/// <see cref="SafetyTier"/> + <see cref="ReferralCategory"/>. An in-code
/// immutable catalog (not YAML) mirroring
/// <c>RunCoach.Api.Modules.Coaching.Sanitization.PatternCatalog</c>: a
/// <see cref="PolicyVersion"/> stamp, a <see cref="Default"/> singleton compiled
/// once at type-load, and a <see cref="ForTesting"/> factory.
/// </summary>
/// <remarks>
/// <para>
/// Scope is the DEC-079 high-risk subset only — Red crisis, Red
/// emergency-referral (cardiac, hip-groin-femoral pain, pregnancy bleeding or
/// contractions), Amber injury, and Amber RED-S / disordered-pattern. The
/// exhaustive DEC-030 pregnancy / youth / chronic taxonomy is deferred to the
/// pre-public-release gate.
/// </para>
/// <para>
/// Rules are ordered by escalation precedence (crisis, then emergency-referral,
/// then injury, then RED-S) so <see cref="SafetyGate"/> can return on first
/// match and get the highest-precedence classification.
/// </para>
/// <para>
/// Recall is prioritised over precision because under-reaction (missing a real
/// crisis or emergency signal) is the hard-failure mode for the MVP-0 self+family
/// audience, while over-reaction is the lesser evil (DEC-079 audience steer).
/// Proximity rules bridge with <c>[\s\S]{0,N}?</c> (not a sentence-terminator
/// class) so a signal spanning a newline or sentence boundary in a multi-line
/// note is still caught. Following <c>PatternCatalog</c>'s false-positive
/// discipline, bare "want to die" (running hyperbole: "I wanted to die on that
/// hill") is omitted, crisis self-injury phrasings require an intent/deliberate
/// qualifier so "tripped and hurt myself" does not escalate, and "cutting"
/// requires "myself" so it never fires on "cutting back my mileage".
/// </para>
/// <para>
/// Thresholds are first-pass values pending eval calibration (Unit 6). Negation
/// handling ("no chest pain" still escalates today) and fine-grained
/// false-positive precision are deliberately deferred to the Unit 6 eval pass
/// against the 5 TestProfiles — a naive negation guard would risk suppressing a
/// real signal ("not going to lie, I want to kill myself"), so it needs
/// calibrated design rather than a rushed regex.
/// </para>
/// </remarks>
internal sealed class SafetyKeywordCatalog
{
    /// <summary>
    /// Catalog version stamped onto safety findings/telemetry. Bump on any rule
    /// change so audit replay can pin the active rule set.
    /// </summary>
    public const string PolicyVersion = "v1.1.0";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(50);

    private SafetyKeywordCatalog(ImmutableArray<SafetyRule> rules)
    {
        Rules = rules;
    }

    /// <summary>
    /// Gets the singleton catalog — rules are stateless and compiled once at
    /// type-load, ordered by escalation precedence.
    /// </summary>
    public static SafetyKeywordCatalog Default { get; } = new(BuildDefaultRules());

    /// <summary>Gets the compiled rules in escalation-precedence order.</summary>
    public ImmutableArray<SafetyRule> Rules { get; }

    /// <summary>
    /// Test-only factory: build a catalog backed by an arbitrary rule set, for
    /// regression tests that exercise edge behavior (e.g. ReDoS timeout
    /// handling) without polluting the production catalog.
    /// </summary>
    /// <param name="rules">The rule set to back the catalog.</param>
    internal static SafetyKeywordCatalog ForTesting(ImmutableArray<SafetyRule> rules) => new(rules);

    private static ImmutableArray<SafetyRule> BuildDefaultRules() =>
        [.. CrisisRules(), .. EmergencyReferralRules(), .. InjuryRules(), .. RedSRules()];

    // Red: crisis (self-harm / suicidal ideation). Bare "want to die" is omitted
    // as running hyperbole; self-injury verbs require an intent/deliberate frame.
    private static ImmutableArray<SafetyRule> CrisisRules() =>
    [
        Rule("SG-C01", SafetyTier.Red, ReferralCategory.Crisis, @"\bkill(?:ing)?\s+myself\b"),
        Rule("SG-C02", SafetyTier.Red, ReferralCategory.Crisis, @"\bend(?:ing)?\s+my\s+(?:own\s+)?life\b"),
        Rule("SG-C03", SafetyTier.Red, ReferralCategory.Crisis, @"\bend(?:ing)?\s+it\s+all\b"),
        Rule("SG-C04", SafetyTier.Red, ReferralCategory.Crisis, @"\bself[\s-]?harm(?:ing|ed)?\b"),
        Rule("SG-C05", SafetyTier.Red, ReferralCategory.Crisis, @"\bsuicidal\b"),
        Rule("SG-C06", SafetyTier.Red, ReferralCategory.Crisis, @"\bsuicide\b"),
        Rule("SG-C07", SafetyTier.Red, ReferralCategory.Crisis, @"\b(?:want(?:s|ing|ed)?|going|trying|urge)\s+to\s+(?:hurt|harm)\s+myself\b"),
        Rule("SG-C08", SafetyTier.Red, ReferralCategory.Crisis, @"\b(?:thinking\s+about\s+(?:hurting|harming)\s+myself|(?:hurt|harm)(?:ing)?\s+myself\s+(?:on\s+purpose|deliberately|intentionally))\b"),
        Rule("SG-C09", SafetyTier.Red, ReferralCategory.Crisis, @"\b(?:cutting\s+myself|(?:want(?:s|ing|ed)?|going|trying|urge)\s+to\s+cut\s+myself|cut\s+myself\s+(?:on\s+purpose|deliberately|intentionally))\b"),
        Rule("SG-C10", SafetyTier.Red, ReferralCategory.Crisis, @"\bdon['\u2019]?t\s+want\s+to\s+(?:live|be\s+here|exist|go\s+on)\b"),
        Rule("SG-C11", SafetyTier.Red, ReferralCategory.Crisis, @"\bbetter\s+off\s+without\s+me\b"),
        Rule("SG-C12", SafetyTier.Red, ReferralCategory.Crisis, @"\bno\s+reason\s+to\s+(?:keep\s+going|go\s+on|carry\s+on|live|be\s+here)\b"),
        Rule("SG-C13", SafetyTier.Red, ReferralCategory.Crisis, @"\bcan['\u2019]?t\s+go\s+on\s+(?:like\s+this|any\s*more)\b"),
        Rule("SG-C14", SafetyTier.Red, ReferralCategory.Crisis, @"\b(?:don['\u2019]?t\s+want\s+to\s+wake\s+up|wish(?:ing)?\s+(?:i\s+)?(?:wouldn|didn)['\u2019]?t\s+wake\s+up)\b"),
    ];

    // Red: emergency referral (cardiac, hip-groin-femoral pain, pregnancy bleeding
    // or contractions). Proximity bridges use [\s\S] to survive multi-line notes.
    private static ImmutableArray<SafetyRule> EmergencyReferralRules() =>
    [
        Rule("SG-E01", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bchest\s+pain\b"),
        Rule("SG-E02", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bchest\b[\s\S]{0,20}?\b(?:tight\w*|pressure|heav\w*|squeez\w*)\b"),
        Rule("SG-E03", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:tight\w*|pressure|squeez\w*|heaviness)\b[\s\S]{0,20}?\bchest\b"),
        Rule("SG-E04", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\birregular\s+(?:heart\s*beat|pulse|heart\s+rhythm)\b"),
        Rule("SG-E05", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:palpitations?|skip(?:ping|ped)\s+(?:a\s+)?beats?|heart\s+(?:\w+\s+){0,2}(?:skip\w*|flutter\w*))\b"),
        Rule("SG-E06", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bpain\b[\s\S]{0,30}?\b(?:left\s+arm|down\s+my\s+arm|jaw|radiat\w*)\b"),
        Rule("SG-E07", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:passed\s+out|fainted|blacked\s+out|syncope|(?:going\s+to|about\s+to|gonna|nearly|almost)\s+(?:faint|pass\s+out|black\s+out))\b"),
        Rule("SG-E08", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bfemoral\b"),
        Rule("SG-E09", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bgroin\b[\s\S]{0,12}?\b(?:pain|hurt\w*|ache\w*)\b"),
        Rule("SG-E10", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:pain|hurt\w*|ache\w*)\b[\s\S]{0,12}?\bgroin\b"),
        Rule("SG-E11", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bhip\b[\s\S]{0,30}?\b(?:worse|worsening|gets?\s+worse|getting\s+worse|every\s+(?:run|step|stride)|deep\s+ache|stress\s+fracture|can['\u2019]?t\s+(?:bear|put)\s+(?:any\s+)?weight)\b"),
        Rule("SG-E12", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:sharp\s+pain|deep\s+ache|stabbing)\b[\s\S]{0,12}?\bhip\b"),
        Rule("SG-E13", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\bvaginal\s+bleeding\b"),
        Rule("SG-E14", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:pregnant|pregnancy|expecting|\d+\s+weeks\s+(?:along|pregnant))\b[\s\S]{0,40}?\b(?:bleed\w*|spotting|contractions?|cramp\w*)\b"),
        Rule("SG-E15", SafetyTier.Red, ReferralCategory.EmergencyReferral, @"\b(?:bleed\w*|spotting|contractions?|cramp\w*)\b[\s\S]{0,40}?\b(?:pregnant|pregnancy|expecting|\d+\s+weeks\s+(?:along|pregnant))\b"),
    ];

    // Amber: injury (sharp / shooting / persistent / worsening pain, or pain that
    // stopped or limited the run via any run-curtailment verb).
    private static ImmutableArray<SafetyRule> InjuryRules() =>
    [
        Rule("SG-I01", SafetyTier.Amber, ReferralCategory.Injury, @"\b(?:sharp|shooting|stabbing)\s+pain\b"),
        Rule("SG-I02", SafetyTier.Amber, ReferralCategory.Injury, @"\bpersistent\s+pain\b"),
        Rule("SG-I03", SafetyTier.Amber, ReferralCategory.Injury, @"\bpain(?:ful)?\b[\s\S]{0,15}?\bwors(?:e|ening)\b"),
        Rule("SG-I04", SafetyTier.Amber, ReferralCategory.Injury, @"\bwors(?:e|ening)\b[\s\S]{0,15}?\b(?:pain(?:ful)?|ache|hurt\w*)\b"),
        Rule("SG-I05", SafetyTier.Amber, ReferralCategory.Injury, @"\b(?:had\s+to\s+(?:stop|walk|quit)|couldn['\u2019]?t\s+(?:finish|continue|keep\s+going)|cut\s+(?:it|the\s+run)\s+short|forced\s+(?:me\s+)?to\s+(?:stop|walk)|gave\s+out)\b[\s\S]{0,40}?\b(?:pain|hurt\w*|injur\w*|knee|ankle|hip|shin|calf|achilles)\b"),
        Rule("SG-I06", SafetyTier.Amber, ReferralCategory.Injury, @"\b(?:pain|hurt\w*|injur\w*)\b[\s\S]{0,30}?\b(?:had\s+to\s+(?:stop|walk|quit)|couldn['\u2019]?t\s+(?:finish|continue)|forced\s+(?:me\s+)?to\s+(?:stop|walk)|gave\s+out)\b"),
        Rule("SG-I07", SafetyTier.Amber, ReferralCategory.Injury, @"\bcan['\u2019]?t\s+(?:walk|run|bear\s+weight|put\s+(?:any\s+)?weight)\b"),
    ];

    // Amber: RED-S / disordered pattern (amenorrhea or absent periods, stress
    // fracture, under-eating, compensatory exercise, running through injury,
    // rest-day distress).
    private static ImmutableArray<SafetyRule> RedSRules() =>
    [
        Rule("SG-R01", SafetyTier.Amber, ReferralCategory.RedS, @"\bamenorrh(?:ea|oea)\b"),
        Rule("SG-R02", SafetyTier.Amber, ReferralCategory.RedS, @"\bmissed\s+(?:\w+\s+){0,3}periods?\b"),
        Rule("SG-R03", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:haven['\u2019]?t\s+had|hasn['\u2019]?t\s+(?:had|come)|lost|stopped\s+having|no\s+(?:more\s+)?)\s+(?:\w+\s+){0,3}periods?\b"),
        Rule("SG-R04", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:my\s+)?periods?\b[\s\S]{0,20}?\b(?:stopped|disappeared|gone|hasn['\u2019]?t\s+come|haven['\u2019]?t\s+come)\b"),
        Rule("SG-R05", SafetyTier.Amber, ReferralCategory.RedS, @"\bstress\s+fracture\b"),
        Rule("SG-R06", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:not\s+eating\s+enough|under[\s-]?eating|not\s+fueling|not\s+eating\s+back|restrict\w*\s+(?:my\s+)?(?:food|eating|calories|intake))\b"),
        Rule("SG-R07", SafetyTier.Amber, ReferralCategory.RedS, @"\bearn(?:ing|ed)?\s+(?:my|the)\s+(?:food|meal|calories|dinner|carbs)\b"),
        Rule("SG-R08", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:run|running|ran)\s+(?:it|them|that)\s+off\b(?![\s\S]{0,8}\b(?:road|trail|track|path|course|back|front|pack)\b)"),
        Rule("SG-R09", SafetyTier.Amber, ReferralCategory.RedS, @"\bburn(?:ing|ed)?\s+(?:it|them|those|the\s+calories)\s+off\b"),
        Rule("SG-R10", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:run|running|ran)\s+through\s+(?:the\s+|my\s+|this\s+)?(?:injury|pain)\b"),
        Rule("SG-R11", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:guilty|anxious|ashamed|panic\w*)\b[\s\S]{0,40}?\b(?:rest\s+day|day\s+off|not\s+running|didn['\u2019]?t\s+run|skip\w*\s+(?:a\s+|my\s+|the\s+)?run)\b"),
        Rule("SG-R12", SafetyTier.Amber, ReferralCategory.RedS, @"\b(?:rest\s+day|day\s+off|not\s+running)\b[\s\S]{0,40}?\b(?:guilty|anxious|ashamed|panic\w*)\b"),
    ];

    private static Regex Compile(string pattern) =>
        new(
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase,
            RegexTimeout);

    private static SafetyRule Rule(string ruleId, SafetyTier tier, ReferralCategory category, string pattern) =>
        new(ruleId, tier, category, Compile(pattern));

    /// <summary>
    /// A single compiled safety rule: a stable id (e.g. "SG-C01") and the
    /// <see cref="SafetyTier"/> + <see cref="ReferralCategory"/> it resolves to,
    /// plus the precompiled word-boundary regex.
    /// </summary>
    /// <param name="RuleId">Stable rule identifier for telemetry (PII-free).</param>
    /// <param name="Tier">The tier this rule resolves to.</param>
    /// <param name="Category">The referral category this rule resolves to.</param>
    /// <param name="Matcher">The compiled, case-insensitive, ReDoS-guarded matcher.</param>
    public sealed record SafetyRule(
        string RuleId,
        SafetyTier Tier,
        ReferralCategory Category,
        Regex Matcher);
}
