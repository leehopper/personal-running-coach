using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Safety;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// A deterministic adaptation classification scenario: a runner profile, a safety
/// tier, and a sequence of logged workouts, paired with the physiologically
/// correct ground-truth escalation level the deterministic engine should resolve
/// on the final step. The ground truth is the scenario author's calibration
/// target — a mismatch in the under-reaction direction is a hard fail (DEC-079),
/// which is how the suite surfaces a mis-tuned threshold constant against the
/// five <c>TestProfiles</c> (Unit 6 calibration).
/// </summary>
/// <param name="Id">Stable scenario identifier (e.g. "restructure.priya.sustained-miss").</param>
/// <param name="Category">The reporting category (derived from the ground-truth level by the <c>AdaptationScenarioLibrary.Classify</c> factory).</param>
/// <param name="ProfileName">The <c>TestProfiles</c> key (sarah / lee / maria / james / priya).</param>
/// <param name="SafetyTier">The safety tier in force (Green for pure classification scenarios).</param>
/// <param name="ExpectedLevel">The ground-truth escalation level for the final step.</param>
/// <param name="Steps">The logged workouts, in order.</param>
internal sealed record EscalationScenario(
    string Id,
    AdaptationEvalCategory Category,
    string ProfileName,
    SafetyTier SafetyTier,
    EscalationLevel ExpectedLevel,
    IReadOnlyList<EscalationScenarioStep> Steps);
