using System.ComponentModel;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The advisory workout draft the intent classifier extracts from a conversational
/// log message (Slice 4B, DEC-085 D3/D4). Its fields align with the SI-unit actuals
/// of <see cref="CreateWorkoutLogRequestDto"/> so a confirmed draft maps directly onto
/// the unchanged Slice 2b create contract — but it carries <b>actuals only</b>, never a
/// prescription: the candidate prescribed-workout match is resolved server-authoritatively
/// at confirm time (the same <c>WorkoutLogService.ResolvePrescriptionAsync</c> path), never
/// from LLM extraction.
/// </summary>
/// <remarks>
/// This is an LLM proposal, advisory until an explicit button-driven Confirm (DEC-085 D4):
/// it never auto-commits. The classifier prompt instructs that a message lacking a
/// resolvable date, distance, or duration be classified <see cref="MessageIntent.Ambiguous"/>
/// (ask) rather than <see cref="MessageIntent.WorkoutLog"/>, so a populated draft always
/// carries the create contract's required actuals; the confirmation card's Edit affordance
/// covers anything else (e.g. open <c>Metrics</c>, which are out of scope for the constrained
/// classifier schema). The <see cref="IdempotencyKey"/> and any <c>Metrics</c> are supplied by
/// the confirm path, not by the LLM.
/// </remarks>
public sealed record StructuredLogDraft
{
    /// <summary>Gets the calendar date the workout occurred on (the prescription anchor).</summary>
    [Description("The calendar date the workout occurred on, as an ISO-8601 date (YYYY-MM-DD), resolved against today's date for relative references like \"this morning\" or \"yesterday\".")]
    public required DateOnly OccurredOn { get; init; }

    /// <summary>Gets the distance covered, in meters.</summary>
    [Description("The distance covered, in meters. Convert from the runner's units (e.g. 5 km = 5000, 3 miles = 4828).")]
    public required double DistanceMeters { get; init; }

    /// <summary>Gets the elapsed time, in seconds.</summary>
    [Description("The elapsed time of the run, in seconds. Convert from the runner's units (e.g. 25 minutes = 1500).")]
    public required double DurationSeconds { get; init; }

    /// <summary>Gets how completely the workout was carried out.</summary>
    [Description("How completely the workout was carried out: Complete (finished as intended), Partial (cut short), or Skipped (not done).")]
    public required CompletionStatus CompletionStatus { get; init; }

    /// <summary>Gets the optional free-text note the runner included; null when none.</summary>
    [Description("Any free-text note the runner included about how it felt or went (e.g. \"legs felt heavy\"). Null when there is none.")]
    public required string? Notes { get; init; }
}
