namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Strongly-typed wrapper around the canonical Slice 1 plan event sequence
/// returned by <see cref="IPlanGenerationService.GeneratePlanAsync"/>. Encodes
/// the documented shape <c>[PlanGenerated, MesoCycleCreated×4, FirstMicroCycleCreated]</c>
/// in the type system so callers cannot accidentally drop, reorder, or
/// mismatch element types when staging the events on a Marten session.
/// </summary>
/// <remarks>
/// <para>
/// Construction validates <c>Mesos.Count == <see cref="ExpectedMesoCount"/></c>
/// (Slice 1 generates weeks 1-4) and that all three fields are non-null.
/// <see cref="ToEvents"/> flattens the wrapper back to the
/// <c>IReadOnlyList&lt;object&gt;</c> shape that <c>session.Events.StartStream</c>
/// expects.
/// </para>
/// </remarks>
/// <param name="Macro">The plan-generation event carrying the macro plan.</param>
/// <param name="Mesos">
/// The four meso-cycle events for weeks 1..4, in order. Must contain exactly
/// <see cref="ExpectedMesoCount"/> entries.
/// </param>
/// <param name="Micro">The first-micro-cycle event with detailed week-1 workouts.</param>
public sealed record PlanEventSequence(
    PlanGenerated Macro,
    IReadOnlyList<MesoCycleCreated> Mesos,
    FirstMicroCycleCreated Micro)
{
    /// <summary>
    /// The required number of meso events per the Slice 1 contract.
    /// </summary>
    public const int ExpectedMesoCount = 4;

    /// <summary>
    /// Gets the plan-generation event carrying the macro plan.
    /// </summary>
    public PlanGenerated Macro { get; init; } = Macro ?? throw new ArgumentNullException(nameof(Macro));

    /// <summary>
    /// Gets the four meso-cycle events for weeks 1..4, in order.
    /// </summary>
    public IReadOnlyList<MesoCycleCreated> Mesos { get; init; } = ValidateMesos(Mesos);

    /// <summary>
    /// Gets the first-micro-cycle event with detailed week-1 workouts.
    /// </summary>
    public FirstMicroCycleCreated Micro { get; init; } = Micro ?? throw new ArgumentNullException(nameof(Micro));

    /// <summary>
    /// Flattens the wrapper into the ordered <c>IReadOnlyList&lt;object&gt;</c>
    /// expected by Marten's <c>session.Events.StartStream</c>.
    /// </summary>
    /// <returns>
    /// The events in canonical order:
    /// <c>[PlanGenerated, MesoCycleCreated×4, FirstMicroCycleCreated]</c>.
    /// </returns>
    public IReadOnlyList<object> ToEvents()
    {
        var events = new List<object>(2 + Mesos.Count) { Macro };
        events.AddRange(Mesos);
        events.Add(Micro);
        return events;
    }

    private static IReadOnlyList<MesoCycleCreated> ValidateMesos(IReadOnlyList<MesoCycleCreated> mesos)
    {
        ArgumentNullException.ThrowIfNull(mesos);
        if (mesos.Count != ExpectedMesoCount)
        {
            throw new ArgumentException(
                $"Expected {ExpectedMesoCount} meso events, got {mesos.Count}.",
                nameof(mesos));
        }

        // Each entry must carry the canonical 1-based week index for its
        // position so ToEvents() emits weeks 1..ExpectedMesoCount in order.
        // A reordered or duplicated meso list would otherwise flatten through
        // ToEvents() and quietly violate the wrapper's contract.
        for (var index = 0; index < mesos.Count; index++)
        {
            var expectedWeek = index + 1;
            if (mesos[index].WeekIndex != expectedWeek)
            {
                throw new ArgumentException(
                    $"Meso events must be ordered for weeks 1..{ExpectedMesoCount}; " +
                    $"position {index} carried WeekIndex={mesos[index].WeekIndex}, expected {expectedWeek}.",
                    nameof(mesos));
            }
        }

        return mesos;
    }
}
