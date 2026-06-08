namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// The kind of plan change an adaptation applied, surfaced to the read-only
/// "Explain-the-change" panel (DEC-079). Distinct from <see cref="EscalationLevel"/>:
/// the kind drives how the panel renders the turn (silent / inline / expandable),
/// while the level records the DEC-012 ladder position. Values are explicitly
/// numbered so reordering members never shifts the stored/serialized integer
/// encoding (matching the <see cref="Safety.SafetyTier"/> convention).
/// </summary>
public enum AdaptationKind
{
    /// <summary>No change — the log was absorbed; no event is appended for this kind.</summary>
    Absorb = 0,

    /// <summary>A deterministic 1–2 workout swap; the panel renders an inline one-liner.</summary>
    Nudge = 1,

    /// <summary>An LLM-authored week restructure; the panel renders an expandable before/after block.</summary>
    Restructure = 2,
}
