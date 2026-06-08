namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The per-user adaptation plan-state in the simplified MVP-0 signal machine
/// (Slice 3 PR2 / Unit 1, DEC-078). Drives the asymmetric enter/exit hysteresis:
/// a restructure moves the state to <see cref="NeedsAdjustment"/> and cannot re-fire
/// until the signal clears the exit threshold and the cooldown elapses. Members are
/// explicitly numbered so reordering never shifts a serialized encoding.
/// </summary>
public enum PlanState
{
    /// <summary>On track — recent logs are within band; absorb with no plan change.</summary>
    OnTrack = 0,

    /// <summary>Minor deviation — a missed key workout or accumulating small misses; micro-adjust territory.</summary>
    MinorDeviation = 1,

    /// <summary>Needs adjustment — a restructure has fired; the hysteresis dead-zone is active.</summary>
    NeedsAdjustment = 2,
}
