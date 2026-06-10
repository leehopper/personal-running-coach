using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// A deterministic safety-gate scenario: a logged-workout note (already past the
/// DEC-059 sanitizer at the caller boundary) and the tier + category the
/// <c>SafetyGate</c> should resolve. Safety scoring is asymmetric in the same
/// spirit as escalation: under-classification (missing a real signal — e.g. a
/// crisis note left Green) is a hard fail; over-classification (a benign note
/// flagged) scores low but does not fail the suite (DEC-079 recall-over-precision).
/// </summary>
/// <param name="Id">Stable scenario identifier (e.g. "safety.crisis.hopeless").</param>
/// <param name="ProfileName">The <c>TestProfiles</c> key the note is attributed to.</param>
/// <param name="Notes">The logged-workout note text the gate scans.</param>
/// <param name="ExpectedTier">The tier the gate should resolve.</param>
/// <param name="ExpectedCategory">The referral category the gate should resolve.</param>
internal sealed record SafetyScenario(
    string Id,
    string ProfileName,
    string Notes,
    SafetyTier ExpectedTier,
    ReferralCategory ExpectedCategory);
