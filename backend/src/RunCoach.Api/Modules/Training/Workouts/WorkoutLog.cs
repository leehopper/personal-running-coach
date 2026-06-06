using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Marten.Metadata;
using RunCoach.Api.Modules.Identity.Entities;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// An immutable historical record of a workout the runner actually did
/// (slice-2b / DEC-072 / DEC-076). Flexible across data richness: required core
/// fields, an open optional-metrics <c>jsonb</c> bag, typed display-only splits,
/// a first-class freeform note, and a nullable server-authoritative prescription
/// snapshot. EF Core entity (the plan stays Marten); tenanted for parity with the
/// rest of the relational schema.
/// </summary>
[Table("WorkoutLog")]
public class WorkoutLog : ITenanted
{
    /// <summary>Gets or sets the primary key.</summary>
    [Key]
    public Guid WorkoutLogId { get; set; }

    /// <summary>Gets or sets the owning runner's user id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the navigation to the owning Identity user.</summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    /// <summary>Gets or sets the Marten conjoined-tenancy parity column (DEC-072). Set by the writer to the user id.</summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the client-supplied idempotency key (DEC-077). Unique per
    /// <c>(UserId, IdempotencyKey)</c>; a replayed create returns the original
    /// row's id instead of inserting a duplicate. The key is a column on the fact
    /// itself, so it persists iff the row does — a failed create leaves the key
    /// reusable with no marker to roll back.
    /// </summary>
    public Guid IdempotencyKey { get; set; }

    /// <summary>Gets or sets the calendar date the run occurred — the prescription-matching anchor (DEC-076).</summary>
    public DateOnly OccurredOn { get; set; }

    /// <summary>Gets or sets the total distance run (value-converted to a meters column).</summary>
    public Distance Distance { get; set; }

    /// <summary>Gets or sets the total elapsed duration (value-converted to a ticks column).</summary>
    public Duration Duration { get; set; }

    /// <summary>Gets or sets how fully the workout was completed.</summary>
    public CompletionStatus CompletionStatus { get; set; }

    /// <summary>Gets or sets the freeform "what happened" note. No server-side length cap.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Gets or sets the open optional-metrics bag as a raw JSON string mapped to <c>jsonb</c>
    /// (DEC-072) — the API owns the JSON; canonical keys live in
    /// <c>WorkoutMetricKeys</c>. Null when no optional metrics were provided.
    /// </summary>
    public string? Metrics { get; set; }

    /// <summary>Gets or sets the typed per-lap splits, stored as a single <c>jsonb</c> column. Null when none.</summary>
    public List<WorkoutSplit>? Splits { get; set; }

    /// <summary>Gets or sets the server-authoritative prescription snapshot, or null when off-plan (DEC-076).</summary>
    public WorkoutPrescriptionSnapshot? Prescription { get; set; }

    /// <summary>Gets or sets the audit timestamp for when the log row was created.</summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>Gets or sets the audit timestamp for when the log row was last modified.</summary>
    public DateTimeOffset ModifiedOn { get; set; }
}
