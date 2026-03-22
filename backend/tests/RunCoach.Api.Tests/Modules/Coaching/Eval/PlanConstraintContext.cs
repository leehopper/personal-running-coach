using Microsoft.Extensions.AI.Evaluation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Context passed to <see cref="PlanConstraintEvaluator"/> via additionalContext.
/// Contains the typed plan records and profile characteristics for constraint checking.
/// </summary>
public sealed class PlanConstraintContext : EvaluationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlanConstraintContext"/> class.
    /// </summary>
    public PlanConstraintContext()
        : base("PlanConstraints", "Plan constraint evaluation context with typed records.")
    {
    }

    /// <summary>Gets the macro plan to evaluate.</summary>
    public MacroPlanOutput? MacroPlan { get; init; }

    /// <summary>Gets the meso week to evaluate.</summary>
    public MesoWeekOutput? MesoWeek { get; init; }

    /// <summary>Gets the workouts to evaluate.</summary>
    public WorkoutOutput[]? Workouts { get; init; }

    /// <summary>Gets the VDOT-derived training paces for pace range checks.</summary>
    public TrainingPaces? TrainingPaces { get; init; }

    /// <summary>Gets the current weekly km for volume ceiling checks.</summary>
    public int? CurrentWeeklyKm { get; init; }

    /// <summary>Gets a value indicating whether this is a beginner profile.</summary>
    public bool IsBeginnerProfile { get; init; }

    /// <summary>Gets a value indicating whether this is an injured profile.</summary>
    public bool IsInjuredProfile { get; init; }
}
