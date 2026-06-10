using RunCoach.Api.Modules.Training.Adaptation;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval.Adaptation;

/// <summary>
/// The result of running an <see cref="EscalationScenario"/> through the real
/// deterministic chain (deviation engine → escalation classifier): the level
/// resolved on the final step, and the per-step levels for diagnostics
/// (consumers derive any per-level counts from <see cref="StepLevels"/>).
/// </summary>
/// <param name="FinalLevel">The escalation level resolved on the last step.</param>
/// <param name="StepLevels">The level resolved at each step, in order.</param>
internal sealed record EscalationScenarioRun(
    EscalationLevel FinalLevel,
    IReadOnlyList<EscalationLevel> StepLevels);
