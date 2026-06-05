using System.ComponentModel.DataAnnotations.Schema;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Server-authoritative, point-in-time snapshot of the prescription a logged run
/// fulfilled (DEC-076). Mapped as an EF Core 10 <b>optional complex type</b>
/// (table-split to real columns on <c>WorkoutLog</c>, not owned-type or JSON) so
/// Slice 3's deterministic deviation engine can compare prescribed-vs-actual
/// without re-resolving a regenerable plan, and so the
/// <c>(SourcePlanId, WeekNumber, DayOfWeek)</c> coordinate is an indexable query.
/// A null <c>Prescription</c> on the entity means the run was off-plan.
/// <see cref="SourcePlanId"/> is the ≥1 EF-required property an optional complex
/// type needs to detect presence.
/// </summary>
public sealed record WorkoutPrescriptionSnapshot
{
    /// <summary>Gets the plan stream id whose week/day slot located this prescription.</summary>
    public Guid SourcePlanId { get; init; }

    /// <summary>Gets the 1-based week index within the source plan.</summary>
    public int WeekNumber { get; init; }

    /// <summary>Gets the day of week, 0 = Sunday through 6 = Saturday.</summary>
    public int DayOfWeek { get; init; }

    /// <summary>Gets the frozen copy of the prescribed workout type.</summary>
    public WorkoutType WorkoutType { get; init; }

    /// <summary>Gets the frozen prescribed total distance.</summary>
    public Distance PrescribedDistance { get; init; }

    /// <summary>Gets the frozen prescribed total duration.</summary>
    public Duration PrescribedDuration { get; init; }

    /// <summary>Gets the frozen prescribed fast (lower sec/km) pace bound.</summary>
    public Pace PrescribedPaceFast { get; init; }

    /// <summary>Gets the frozen prescribed slow (higher sec/km) pace bound.</summary>
    public Pace PrescribedPaceSlow { get; init; }

    /// <summary>
    /// Gets the prescribed pace as a <see cref="PaceRange"/> value object — a computed
    /// view over the two stored bounds, not mapped to its own column(s) (a single
    /// value converter cannot span two columns). Ignored by EF in
    /// <c>WorkoutLogConfiguration</c>.
    /// </summary>
    [NotMapped]
    public PaceRange PrescribedPace => new(PrescribedPaceFast, PrescribedPaceSlow);
}
