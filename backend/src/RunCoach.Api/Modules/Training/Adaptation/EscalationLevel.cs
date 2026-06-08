using System.Text.Json.Serialization;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The DEC-012 escalation-ladder level the deterministic adaptation gate resolves
/// for a logged workout (0-indexed canon). Levels 0–1 are handled in code with no
/// LLM call; Level 2 (restructure) is the first level that invokes the coaching
/// LLM. Slice 3 resolves levels 0–2 (a deterministic Level-3 signal is folded into
/// L2 for MVP-0); Level 4 (plan overhaul, requires explicit user confirmation) is
/// deferred to Slice 4. Serialized by name so the durable event payload survives
/// any future member reordering.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EscalationLevel>))]
public enum EscalationLevel
{
    /// <summary>Level 0 — actuals within band; log only, no plan change, no turn.</summary>
    Absorb = 0,

    /// <summary>Level 1 — deterministic micro-adjust (swap 1–2 workouts); no LLM.</summary>
    MicroAdjust = 1,

    /// <summary>Level 2 — week restructure; the first level requiring an LLM call.</summary>
    Restructure = 2,

    /// <summary>Level 3 — phase reconsider; folded into <see cref="Restructure"/> for MVP-0.</summary>
    PhaseReconsider = 3,

    /// <summary>Level 4 — plan overhaul requiring explicit user confirmation; deferred to Slice 4.</summary>
    PlanOverhaul = 4,
}
