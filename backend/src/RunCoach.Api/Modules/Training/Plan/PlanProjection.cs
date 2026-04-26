using Marten.Events.Aggregation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Plan.Models;

namespace RunCoach.Api.Modules.Training.Plan;

/// <summary>
/// Inline single-stream projection that materializes a Plan stream into a
/// <see cref="PlanProjectionDto"/> Marten document the frontend renders directly
/// (spec 13 § Unit 2, R02.3). Marten's codegen wires each <c>Apply</c> overload
/// below to its event type via pattern-matching at startup; the document is
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
public sealed class PlanProjection : SingleStreamProjection<PlanProjectionDto, Guid>
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
    public static void Apply(MesoCycleCreated @event, PlanProjectionDto dto)
    {
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
}
