using Marten.Events.Aggregation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Inline single-stream projection that materializes a Plan stream into a
/// <see cref="PlanProjectionDto"/> Marten document the frontend renders directly
/// (spec 13 § Unit 2, R02.3). Marten 9's compile-time JasperFx source generator
/// dispatches each <c>Apply</c>/<c>Create</c> convention method below to its event
/// type, which is why this class must be declared <c>partial</c>; the document is
/// upserted on the same <c>IDocumentSession</c> as the event append, preserving
/// atomicity for the calling handler (Unit 1 onboarding terminal-branch handler
/// in Slice 1, Unit 5 regenerate handler later).
/// </summary>
/// <remarks>
/// <para>
/// Registered with <c>ProjectionLifecycle.Inline</c> in
/// <see cref="Infrastructure.MartenConfiguration"/>. Reading the projection is
/// a single <c>session.LoadAsync&lt;PlanProjectionDto&gt;(planId)</c> call from
/// the rendering controller - no LLM cost, no event replay.
/// </para>
/// <para>
/// Slice 1 consumes <see cref="PlanGenerated"/> (stream-creation),
/// <see cref="MesoCycleCreated"/> (x4), and <see cref="FirstMicroCycleCreated"/>.
/// Future slices add <c>PlanAdaptedFromLog</c> (Slice 3) and
/// <c>PlanRestructuredFromConversation</c> (Slice 4) as additive
/// <c>Apply</c> methods - no breaking changes for the Slice 1 frontend.
/// </para>
/// </remarks>
public sealed partial class PlanProjection : SingleStreamProjection<PlanProjectionDto, Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanProjection"/> class. Marten's
    /// inline registration constructs the projection at startup; no DI wiring is
    /// needed because pure event-application logic carries no dependencies.
    /// </summary>
    public PlanProjection()
    {
        // Marten infers the projection name from the type, but pinning it here
        // keeps the schema artifact name stable across rename refactors that
        // would otherwise break production projection-state rows on a redeploy.
        Name = "PlanProjection";
    }

    /// <summary>
    /// Initializes the read model from the stream-creation event. Populates the
    /// macro tier, provenance fields, and the <see cref="PlanGenerated.PreviousPlanId"/>
    /// audit link; meso + micro slots stay at their default empty collections
    /// until the subsequent apply methods land.
    /// </summary>
    /// <param name="event">The stream-creation event.</param>
    /// <returns>The initial <see cref="PlanProjectionDto"/> document for the stream.</returns>
    public static PlanProjectionDto Create(PlanGenerated @event)
    {
        return new PlanProjectionDto
        {
            PlanId = @event.PlanId,
            UserId = @event.UserId,
            GeneratedAt = @event.GeneratedAt,
            PlanStartDate = @event.PlanStartDate,
            PromptVersion = @event.PromptVersion,
            ModelId = @event.ModelId,
            PreviousPlanId = @event.PreviousPlanId,
            Macro = @event.Macro,
            MesoWeeks = Array.Empty<MesoWeekOutput>(),
            MicroWorkoutsByWeek = new Dictionary<int, MicroWorkoutListOutput>(),
        };
    }

    /// <summary>
    /// Appends the meso week to <see cref="PlanProjectionDto.MesoWeeks"/> in
    /// <see cref="MesoCycleCreated.WeekIndex"/> order. The four meso events
    /// emitted in Slice 1 always arrive in ascending week order from
    /// <c>IPlanGenerationService</c>, but this apply method tolerates
    /// out-of-order arrival defensively by replacing-or-inserting at the
    /// correct slot.
    /// </summary>
    /// <param name="event">The meso-tier event for one week of the plan.</param>
    /// <param name="dto">The projection document being mutated.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="MesoCycleCreated.WeekIndex"/> is less than 1.
    /// Week indices are 1-based by spec; a non-positive value indicates a
    /// malformed event that must surface as a transaction failure rather than
    /// silently corrupting <see cref="PlanProjectionDto.MesoWeeks"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="MesoCycleCreated.WeekIndex"/> and
    /// <see cref="MesoWeekOutput.WeekNumber"/> on the embedded payload disagree.
    /// The find-or-replace logic below keys by <c>WeekIndex</c> but writes the
    /// payload (which carries its own <c>WeekNumber</c>); divergence between
    /// them would leave the read model with a slot indexed under one number
    /// and rendered under another.
    /// </exception>
    public static void Apply(MesoCycleCreated @event, PlanProjectionDto dto)
    {
        if (@event.WeekIndex < 1)
        {
            // ParamName binds to the method parameter (@event) rather than the
            // offending property (WeekIndex) - CA2208 / SonarAnalyzer S3928
            // both reject any name not in the method's signature, so the
            // precision is carried in the message instead.
            throw new ArgumentOutOfRangeException(
                nameof(@event),
                @event.WeekIndex,
                $"{nameof(@event)}.{nameof(MesoCycleCreated.WeekIndex)} must be 1-based (got {@event.WeekIndex}).");
        }

        if (@event.Meso.WeekNumber != @event.WeekIndex)
        {
            throw new InvalidOperationException(
                $"Meso payload week mismatch: event WeekIndex={@event.WeekIndex} but Meso.WeekNumber={@event.Meso.WeekNumber}.");
        }

        var weeks = dto.MesoWeeks.ToList();
        var existingIndex = weeks.FindIndex(w => w.WeekNumber == @event.WeekIndex);
        if (existingIndex >= 0)
        {
            weeks[existingIndex] = @event.Meso;
        }
        else
        {
            weeks.Add(@event.Meso);
        }

        // Stable order keeps the frontend's array indexing predictable across
        // any future re-projection runs.
        weeks.Sort((a, b) => a.WeekNumber.CompareTo(b.WeekNumber));
        dto.MesoWeeks = weeks;
    }

    /// <summary>
    /// Records the week-1 detailed workout list onto
    /// <see cref="PlanProjectionDto.MicroWorkoutsByWeek"/>. Slice 1 only emits
    /// micro detail for week 1; later slices add further weeks via additional
    /// event types, which append to this dictionary without re-projection.
    /// </summary>
    /// <param name="event">The micro-tier event for week 1.</param>
    /// <param name="dto">The projection document being mutated.</param>
    public static void Apply(FirstMicroCycleCreated @event, PlanProjectionDto dto)
    {
        var map = new Dictionary<int, MicroWorkoutListOutput>(dto.MicroWorkoutsByWeek)
        {
            [1] = @event.Micro,
        };
        dto.MicroWorkoutsByWeek = map;
    }

    /// <summary>
    /// Applies a Slice 3 adaptation (DEC-079) to the plan read model: revises meso
    /// <see cref="MesoWeekOutput.WeeklyTargetKm"/> for upcoming weeks and swaps
    /// current-week micro workouts from the event's structured
    /// <see cref="PlanAdaptedFromLog.Diff"/>. Mutates <paramref name="dto"/> in place
    /// following the established collection-reassign idiom. Per the spec non-goal,
    /// it never synthesizes micro detail for a week that has none today — a workout
    /// change targeting an unpopulated week is skipped, a meso target change is not.
    /// </summary>
    /// <param name="event">The adaptation event appended to the plan stream.</param>
    /// <param name="dto">The projection document being mutated.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any change in the diff carries a non-positive week number. Week
    /// numbers are 1-based by spec; a malformed value must fail the transaction
    /// rather than silently corrupt the read model.
    /// </exception>
    public static void Apply(PlanAdaptedFromLog @event, PlanProjectionDto dto)
    {
        // Validate week numbers across both change sets up front so a malformed
        // diff fails the whole transaction rather than partially mutating the
        // read model — same fail-loud contract as Apply(MesoCycleCreated, ...).
        foreach (var change in @event.Diff.WeeklyTargetChanges)
        {
            ThrowIfNonPositiveWeek(change.WeekNumber);
        }

        foreach (var change in @event.Diff.WorkoutChanges)
        {
            ThrowIfNonPositiveWeek(change.WeekNumber);
        }

        ApplyWeeklyTargetChanges(@event.Diff.WeeklyTargetChanges, dto);
        ApplyWorkoutChanges(@event.Diff.WorkoutChanges, dto);
    }

    private static void ApplyWeeklyTargetChanges(
        IReadOnlyList<WeeklyTargetChange> changes,
        PlanProjectionDto dto)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var weeks = dto.MesoWeeks.ToList();
        foreach (var change in changes)
        {
            var index = weeks.FindIndex(w => w.WeekNumber == change.WeekNumber);
            if (index >= 0)
            {
                weeks[index] = weeks[index] with { WeeklyTargetKm = change.AfterWeeklyTargetKm };
            }

            // A target change for a week not present in MesoWeeks is skipped — the
            // meso tier is the authoritative week list; the projection never
            // synthesizes weeks the plan-generation flow did not emit.
        }

        dto.MesoWeeks = weeks;
    }

    private static void ApplyWorkoutChanges(
        IReadOnlyList<WorkoutChange> changes,
        PlanProjectionDto dto)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var map = new Dictionary<int, MicroWorkoutListOutput>(dto.MicroWorkoutsByWeek);
        foreach (var group in changes.GroupBy(c => c.WeekNumber))
        {
            // Only weeks that already carry micro detail are edited. The
            // adaptation never synthesizes future micro weeks (DEC-079 / spec
            // non-goal); today only week 1 is populated, so a change targeting an
            // unpopulated week is dropped here rather than inventing a week.
            if (!map.TryGetValue(group.Key, out var micro))
            {
                continue;
            }

            var workouts = micro.Workouts.ToList();
            foreach (var change in group)
            {
                // A removal (null After) is not modeled this slice — skip it.
                if (change.After is null)
                {
                    continue;
                }

                var workoutIndex = workouts.FindIndex(w => w.DayOfWeek == change.DayOfWeek);
                if (workoutIndex >= 0)
                {
                    workouts[workoutIndex] = change.After;
                }
                else
                {
                    workouts.Add(change.After);
                }
            }

            map[group.Key] = micro with { Workouts = [.. workouts] };
        }

        dto.MicroWorkoutsByWeek = map;
    }

    private static void ThrowIfNonPositiveWeek(int weekNumber)
    {
        if (weekNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekNumber),
                weekNumber,
                $"PlanAdaptedFromLog diff week number must be 1-based (got {weekNumber}).");
        }
    }
}
