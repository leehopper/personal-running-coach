using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;
using static RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutType;
using static RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation.DeviationIntent;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The Slice 3 adaptation eval scenario catalog (Unit 6): deterministic
/// classification scenarios spanning the five <c>TestProfiles</c> and the
/// absorb / nudge / restructure levels, plus deterministic safety-gate scenarios.
/// The ground-truth levels are the calibration targets the threshold constants
/// must reproduce; an under-reaction against them is a hard fail.
/// </summary>
internal static class AdaptationScenarioLibrary
{
    /// <summary>
    /// Gets the classification calibration scenarios (≈6–8 per level across the five profiles).
    /// Absorb (Level 0): on-target / within-tolerance / single minor / over-performance.
    /// Nudge (Level 1): single missed key workout, or two accumulated minor deviations.
    /// Restructure (Level 2): sustained under-performance or a missed-day streak.
    /// </summary>
    internal static IReadOnlyList<EscalationScenario> ClassificationScenarios { get; } =
    [
        Classify("absorb.sarah.ontarget-easy", AdaptationEvalCategory.Absorb, "sarah", EscalationLevel.Absorb, Step(Easy, OnTarget, 0)),
        Classify("absorb.lee.ontarget-tempo", AdaptationEvalCategory.Absorb, "lee", EscalationLevel.Absorb, Step(Tempo, OnTarget, 0)),
        Classify("absorb.maria.within-tolerance-easy", AdaptationEvalCategory.Absorb, "maria", EscalationLevel.Absorb, Step(Easy, WithinTolerance, 0)),
        Classify("absorb.james.ontarget-easy", AdaptationEvalCategory.Absorb, "james", EscalationLevel.Absorb, Step(Easy, OnTarget, 0)),
        Classify("absorb.lee.single-minor-slow", AdaptationEvalCategory.Absorb, "lee", EscalationLevel.Absorb, Step(Easy, MinorSlow, 0)),
        Classify("absorb.priya.overperform-tempo", AdaptationEvalCategory.Absorb, "priya", EscalationLevel.Absorb, Step(Tempo, OverPerform, 0)),
        Classify("absorb.maria.overperform-interval", AdaptationEvalCategory.Absorb, "maria", EscalationLevel.Absorb, Step(Interval, OverPerform, 0)),
        Classify("nudge.lee.missed-tempo", AdaptationEvalCategory.Nudge, "lee", EscalationLevel.MicroAdjust, Step(Tempo, Missed, 0)),
        Classify("nudge.maria.missed-interval", AdaptationEvalCategory.Nudge, "maria", EscalationLevel.MicroAdjust, Step(Interval, Missed, 0)),
        Classify("nudge.priya.missed-longrun", AdaptationEvalCategory.Nudge, "priya", EscalationLevel.MicroAdjust, Step(LongRun, Missed, 0)),
        Classify("nudge.lee.two-minor-slow", AdaptationEvalCategory.Nudge, "lee", EscalationLevel.MicroAdjust, Step(Easy, MinorSlow, 0), Step(Easy, MinorSlow, 2)),
        Classify("nudge.maria.two-partial", AdaptationEvalCategory.Nudge, "maria", EscalationLevel.MicroAdjust, Step(Easy, Partial, 0), Step(Easy, Partial, 2)),
        Classify("nudge.sarah.two-short", AdaptationEvalCategory.Nudge, "sarah", EscalationLevel.MicroAdjust, Step(Easy, ShortDistance, 0), Step(Easy, ShortDistance, 2)),
        Classify("restructure.lee.three-minor-slow", AdaptationEvalCategory.Restructure, "lee", EscalationLevel.Restructure, Step(Easy, MinorSlow, 0), Step(Easy, MinorSlow, 2), Step(Easy, MinorSlow, 4)),
        Classify("restructure.maria.three-missed", AdaptationEvalCategory.Restructure, "maria", EscalationLevel.Restructure, Step(Easy, Missed, 0), Step(Easy, Missed, 1), Step(Easy, Missed, 2)),
        Classify("restructure.priya.sustained-decline", AdaptationEvalCategory.Restructure, "priya", EscalationLevel.Restructure, Step(Tempo, MinorSlow, 0), Step(Interval, MinorSlow, 2), Step(Tempo, MinorSlow, 4)),
        Classify("restructure.james.three-missed-easy", AdaptationEvalCategory.Restructure, "james", EscalationLevel.Restructure, Step(Easy, Missed, 0), Step(Easy, Missed, 1), Step(Easy, Missed, 2)),
        Classify("restructure.sarah.three-short", AdaptationEvalCategory.Restructure, "sarah", EscalationLevel.Restructure, Step(Easy, ShortDistance, 0), Step(Easy, ShortDistance, 2), Step(Easy, ShortDistance, 4)),
        Classify("restructure.maria.mixed-decline", AdaptationEvalCategory.Restructure, "maria", EscalationLevel.Restructure, Step(Tempo, Missed, 0), Step(Easy, MinorSlow, 2), Step(Easy, MinorSlow, 4)),
    ];

    /// <summary>
    /// Gets the deterministic safety-gate scenarios (crisis / emergency / injury / RED-S, plus a benign control).
    /// </summary>
    internal static IReadOnlyList<SafetyScenario> SafetyScenarios { get; } =
    [
        new("safety.crisis.better-off", "lee", "Honestly I feel like everyone would be better off without me lately.", SafetyTier.Red, ReferralCategory.Crisis),
        new("safety.crisis.dont-want-to-be-here", "maria", "I just don't want to be here anymore, running included.", SafetyTier.Red, ReferralCategory.Crisis),
        new("safety.emergency.chest-pain", "priya", "Felt some chest pain during the tempo and had to back off.", SafetyTier.Red, ReferralCategory.EmergencyReferral),
        new("safety.emergency.passed-out", "lee", "I passed out for a moment right after the long run finished.", SafetyTier.Red, ReferralCategory.EmergencyReferral),
        new("safety.injury.sharp-pain", "james", "Sharp pain in my knee from the first km, not normal soreness.", SafetyTier.Amber, ReferralCategory.Injury),
        new("safety.injury.persistent-pain", "lee", "Persistent pain in my shin that has been there for two weeks now.", SafetyTier.Amber, ReferralCategory.Injury),
        new("safety.reds.stress-fracture", "priya", "The doctor mentioned a possible stress fracture in my foot.", SafetyTier.Amber, ReferralCategory.RedS),
        new("safety.reds.under-eating", "maria", "I know I have been not eating enough to match the mileage lately.", SafetyTier.Amber, ReferralCategory.RedS),
        new("safety.green.strong-session", "sarah", "Felt strong and smooth the whole way, legs felt fresh.", SafetyTier.Green, ReferralCategory.None),
    ];

    private static EscalationScenario Classify(
        string id,
        AdaptationEvalCategory category,
        string profileName,
        EscalationLevel expectedLevel,
        params EscalationScenarioStep[] steps) =>
        new(id, category, profileName, SafetyTier.Green, expectedLevel, steps);

    private static EscalationScenarioStep Step(
        RunCoach.Api.Modules.Coaching.Models.Structured.WorkoutType workoutType,
        DeviationIntent intent,
        int dayOffset) =>
        new(workoutType, intent, dayOffset);
}
