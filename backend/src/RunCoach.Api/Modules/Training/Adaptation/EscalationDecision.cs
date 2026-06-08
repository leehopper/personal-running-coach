namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The deterministic escalation classifier's output (Slice 3 PR2 / Unit 1): the
/// resolved DEC-012 <see cref="EscalationLevel"/>, the panel-facing
/// <see cref="AdaptationKind"/>, and the advanced <see cref="AdaptationSignalState"/>
/// the caller persists for the next evaluation.
/// </summary>
/// <param name="EscalationLevel">The resolved DEC-012 ladder position (Absorb / MicroAdjust / Restructure).</param>
/// <param name="AdaptationKind">The render discriminator (Absorb / Nudge / Restructure).</param>
/// <param name="NextState">The signal state to carry into the next evaluation.</param>
public sealed record EscalationDecision(
    EscalationLevel EscalationLevel,
    AdaptationKind AdaptationKind,
    AdaptationSignalState NextState);
