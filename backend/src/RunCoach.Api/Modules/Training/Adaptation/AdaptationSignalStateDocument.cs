using JasperFx;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Marten document persisting one plan's <see cref="AdaptationSignalState"/> between
/// adaptation evaluations (DEC-078 resolution: a small persisted document, not a
/// recompute-per-evaluation over the recent-log window). Keyed by <see cref="PlanId"/>
/// so a regenerated plan starts from <see cref="AdaptationSignalState.Initial"/>.
/// The evaluation handler loads it and stores the classifier's next state on the
/// SAME Marten session as its event appends, so the signal-state write commits — or
/// rolls back — atomically with the events it justified. Tenant scoping comes from
/// Marten's conjoined tenancy (<c>Policies.AllDocumentsAreMultiTenanted()</c> in
/// <c>MartenConfiguration</c>), matching every other document in the store.
/// </summary>
/// <param name="PlanId">The plan stream id this signal state belongs to. Marten document identity.</param>
/// <param name="PlanState">The stored plan-state in the hysteresis machine.</param>
/// <param name="RollingDeviationScore">The stored rolling deviation accumulator.</param>
/// <param name="ConsecutiveMissedDays">The stored run of consecutive skipped days.</param>
/// <param name="LastAdaptationOn">The stored day the most recent restructure fired; <c>null</c> until the first restructure.</param>
// Public visibility is required by Marten — document types must be scoped as
// 'public' to pass through the JasperFx codegen pipeline, even with
// InternalsVisibleTo (same constraint as IdempotencyMarker).
public sealed record AdaptationSignalStateDocument(
    [property: Identity] Guid PlanId,
    PlanState PlanState,
    double RollingDeviationScore,
    int ConsecutiveMissedDays,
    DateOnly? LastAdaptationOn)
{
    /// <summary>
    /// Captures a validated in-memory state into its persistable document shape
    /// for the given plan. The inverse of <see cref="ToState"/>.
    /// </summary>
    /// <param name="planId">The plan stream id to key the document by.</param>
    /// <param name="state">The signal state to persist.</param>
    /// <returns>The document ready to <c>session.Store</c>.</returns>
    public static AdaptationSignalStateDocument From(Guid planId, AdaptationSignalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new AdaptationSignalStateDocument(
            planId,
            state.PlanState,
            state.RollingDeviationScore,
            state.ConsecutiveMissedDays,
            state.LastAdaptationOn);
    }

    /// <summary>
    /// Rehydrates the in-memory signal state through
    /// <see cref="AdaptationSignalState.Create"/> — the validating deserialization
    /// boundary. Marten materializes this document straight from JSONB without
    /// running any domain validation, so this is the single place a stored row is
    /// clamped back into the score band and its structural invariants re-checked
    /// before the classifier consumes it.
    /// </summary>
    /// <returns>The validated <see cref="AdaptationSignalState"/>.</returns>
    public AdaptationSignalState ToState() =>
        AdaptationSignalState.Create(
            PlanState,
            RollingDeviationScore,
            ConsecutiveMissedDays,
            LastAdaptationOn);
}
