namespace RunCoach.Api.Modules.Coaching.Adaptation;

/// <summary>
/// Pattern-B-invariant violation taxonomy for <see cref="PlanAdaptationOutput"/>. Stable
/// enum values — never reorder.
/// </summary>
public enum PlanAdaptationOutputValidationViolation
{
    /// <summary>The invariant holds — output is valid.</summary>
    None = 0,

    /// <summary>Both <c>NudgePatch</c> and <c>RestructurePlan</c> are non-null; at most one slot may be filled.</summary>
    MultipleSlots = 1,

    /// <summary>The populated slot (or its absence) does not match the <c>AdaptationKind</c> discriminator.</summary>
    SlotKindMismatch = 2,

    /// <summary>The <c>SafetyTier</c> is not Green yet <c>NetLoadDelta</c> is positive (GATE-BEFORE-INCREASE).</summary>
    LoadIncreaseUnderNonGreenTier = 3,

    /// <summary>A load-reducing restructure (<c>NetLoadDelta &lt; 0</c>) omits a forward path / return trajectory.</summary>
    LoadReducingRestructureMissingForwardPath = 4,
}
