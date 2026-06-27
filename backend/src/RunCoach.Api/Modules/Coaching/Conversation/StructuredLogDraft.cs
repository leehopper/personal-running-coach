using System.ComponentModel;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// The advisory workout draft the intent classifier extracts from a conversational
/// log message (Slice 4B, DEC-085 D3/D4). It captures the runner's actuals in the units
/// they stated — distance value + unit, duration as h/m/s components — and the
/// deterministic <see cref="WorkoutDraftUnitConverter"/> performs the SI conversion
/// server-side when <see cref="StructuredLogDraftMapper"/> maps a confirmed draft onto the
/// unchanged Slice 2b <see cref="CreateWorkoutLogRequestDto"/> create contract. The LLM
/// never converts units — distance/time conversions are deterministic computation that
/// belongs in the unit-tested computation layer. It carries <b>actuals only</b>, never a
/// prescription:
/// the candidate prescribed-workout match is resolved server-authoritatively at confirm
/// time (the same <c>WorkoutLogService.ResolvePrescriptionAsync</c> path), never from LLM
/// extraction.
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

    /// <summary>Gets the distance magnitude the runner stated, in <see cref="DistanceUnit"/> units.</summary>
    [Description("The distance the runner stated, as a number in the unit named by distance_unit. Report exactly what the runner said (e.g. 5 for \"5 km\", 3.1 for \"3.1 miles\"). Do not convert units yourself.")]
    public required double DistanceValue { get; init; }

    /// <summary>Gets the unit the runner stated the distance in; the server converts to meters.</summary>
    [Description("The unit the runner stated the distance in: Kilometers, Miles, or Meters.")]
    public required RunnerDistanceUnit DistanceUnit { get; init; }

    /// <summary>Gets the whole-hours component of the runner-stated elapsed time.</summary>
    [Description("The whole hours of the run's elapsed time (0 if under an hour). Report the components the runner stated; do not convert to seconds.")]
    public required int DurationHours { get; init; }

    /// <summary>Gets the whole-minutes component of the runner-stated elapsed time.</summary>
    [Description("The whole minutes of the run's elapsed time, 0 to 59 (e.g. 25 for \"25 minutes\", 30 for \"1 hour 30 minutes\").")]
    public required int DurationMinutes { get; init; }

    /// <summary>Gets the whole-seconds component of the runner-stated elapsed time.</summary>
    [Description("The whole seconds of the run's elapsed time, 0 to 59 (e.g. 30 for \"22:30\"). Use 0 when the runner gave no seconds.")]
    public required int DurationSeconds { get; init; }

    /// <summary>Gets how completely the workout was carried out.</summary>
    [Description("How completely the workout was carried out: Complete (finished as intended), Partial (cut short), or Skipped (not done).")]
    public required CompletionStatus CompletionStatus { get; init; }

    /// <summary>Gets the optional free-text note the runner included; null when none.</summary>
    [Description("Any free-text note the runner included about how it felt or went (e.g. \"legs felt heavy\"). Null when there is none.")]
    public required string? Notes { get; init; }
}
