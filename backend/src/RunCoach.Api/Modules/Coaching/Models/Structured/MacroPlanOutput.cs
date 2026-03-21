using System.ComponentModel;

namespace RunCoach.Api.Modules.Coaching.Models.Structured;

/// <summary>
/// Structured output record for a periodized macro training plan.
/// Root level: 5 properties, nesting depth 2 (MacroPlanOutput -> PlanPhaseOutput).
/// </summary>
public sealed record MacroPlanOutput
{
    /// <summary>
    /// Gets the total number of weeks in the training plan.
    /// </summary>
    [Description("Total number of weeks in the training plan.")]
    public required int TotalWeeks { get; init; }

    /// <summary>
    /// Gets the name of the goal race or training objective.
    /// </summary>
    [Description("The name of the goal race or training objective, such as 'Half Marathon' or 'General Fitness'.")]
    public required string GoalDescription { get; init; }

    /// <summary>
    /// Gets the periodized phases that make up the plan.
    /// </summary>
    [Description("The periodized training phases that make up this plan.")]
    public required PlanPhaseOutput[] Phases { get; init; }

    /// <summary>
    /// Gets the overall coaching rationale explaining the plan design.
    /// </summary>
    [Description("Overall coaching rationale explaining why this plan is structured this way.")]
    public required string Rationale { get; init; }

    /// <summary>
    /// Gets any important warnings or prerequisites for the athlete.
    /// </summary>
    [Description("Important warnings or prerequisites the athlete should be aware of before starting.")]
    public required string Warnings { get; init; }
}
