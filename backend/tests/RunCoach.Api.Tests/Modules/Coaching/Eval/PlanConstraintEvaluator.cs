using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using RunCoach.Api.Modules.Coaching.Models.Structured;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Tests.Modules.Coaching.Eval;

/// <summary>
/// Deterministic evaluator for training plan constraints.
/// Checks typed plan records against VDOT-derived pace zones, volume limits,
/// rest day counts, workout type restrictions, and duration limits.
/// Does NOT make any API calls — all checks are local computation.
/// </summary>
public sealed class PlanConstraintEvaluator : IEvaluator
{
    /// <summary>Metric name for plan constraint violations.</summary>
    public const string ViolationsMetricName = "PlanConstraintViolations";

    /// <summary>Metric name for the overall pass/fail score.</summary>
    public const string ScoreMetricName = "PlanConstraintScore";

    /// <inheritdoc />
    public IReadOnlyCollection<string> EvaluationMetricNames { get; } =
        [ViolationsMetricName, ScoreMetricName];

    /// <summary>
    /// Evaluates plan constraints directly with typed data.
    /// Returns a list of violation messages (empty = all constraints pass).
    /// </summary>
    public static List<string> Evaluate(PlanConstraintContext context)
    {
        var violations = new List<string>();

        if (context.MacroPlan is not null)
        {
            CheckMacroPlan(context.MacroPlan, violations);
        }

        if (context.MesoWeek is not null)
        {
            CheckMesoWeek(context.MesoWeek, context.CurrentWeeklyKm, context.IsBeginnerProfile, violations);
        }

        if (context.Workouts is not null)
        {
            CheckWorkouts(context.Workouts, context.TrainingPaces, context.IsBeginnerProfile, context.IsInjuredProfile, violations);
        }

        return violations;
    }

    /// <summary>
    /// Evaluates a plan via the IEvaluator interface.
    /// Expects plan data in <paramref name="additionalContext"/> as <see cref="PlanConstraintContext"/>.
    /// </summary>
    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        var context = additionalContext?
            .OfType<PlanConstraintContext>()
            .FirstOrDefault();

        if (context is null)
        {
            var errorMetric = new NumericMetric(ScoreMetricName, value: 0, reason: "No PlanConstraintContext provided.");
            errorMetric.Interpretation = new EvaluationMetricInterpretation(
                EvaluationRating.Unacceptable,
                failed: true,
                reason: "Missing context.");
            return new ValueTask<EvaluationResult>(new EvaluationResult(errorMetric));
        }

        var violations = Evaluate(context);
        return new ValueTask<EvaluationResult>(BuildResult(violations));
    }

    private static void CheckMacroPlan(MacroPlanOutput plan, List<string> violations)
    {
        if (plan.TotalWeeks < 4)
        {
            violations.Add($"MacroPlan.TotalWeeks is {plan.TotalWeeks}, expected at least 4.");
        }

        if (plan.Phases.Length == 0)
        {
            violations.Add("MacroPlan.Phases is empty — expected at least one phase.");
        }
    }

    private static void CheckMesoWeek(MesoWeekOutput week, int? currentWeeklyKm, bool isBeginner, List<string> violations)
    {
        if (week.Days.Length != 7)
        {
            violations.Add($"MesoWeek.Days has {week.Days.Length} entries, expected 7.");
        }

        if (currentWeeklyKm.HasValue && currentWeeklyKm.Value > 0)
        {
            var maxKm = (int)(currentWeeklyKm.Value * 1.10);
            if (week.WeeklyTargetKm > maxKm)
            {
                violations.Add($"MesoWeek.WeeklyTargetKm ({week.WeeklyTargetKm}) exceeds 10% ceiling ({maxKm}) from current {currentWeeklyKm.Value} km/week.");
            }
        }

        var restDayCount = week.Days.Count(d => d.SlotType == DaySlotType.Rest);
        if (isBeginner && restDayCount < 2)
        {
            violations.Add($"Beginner profile has only {restDayCount} rest day(s) — expected at least 2.");
        }
    }

    private static void CheckWorkouts(WorkoutOutput[] workouts, TrainingPaces? paces, bool isBeginner, bool isInjured, List<string> violations)
    {
        foreach (var workout in workouts)
        {
            if (isBeginner && workout.WorkoutType is WorkoutType.Interval or WorkoutType.Tempo)
            {
                violations.Add($"Beginner assigned {workout.WorkoutType} workout '{workout.Title}'.");
            }

            if (isInjured)
            {
                if (workout.TargetDurationMinutes > 20)
                {
                    violations.Add($"Injured profile workout '{workout.Title}' duration {workout.TargetDurationMinutes}min > 20min limit.");
                }

                if (workout.WorkoutType != WorkoutType.Easy)
                {
                    violations.Add($"Injured profile workout '{workout.Title}' is {workout.WorkoutType} — expected Easy.");
                }
            }

            if (paces is not null)
            {
                CheckPaceRanges(workout, paces, violations);
            }
        }
    }

    private static void CheckPaceRanges(WorkoutOutput workout, TrainingPaces paces, List<string> violations)
    {
        var easyRange = paces.EasyPaceRange;
        if (workout.TargetPaceEasySecPerKm > 0 && easyRange.MinPerKm > TimeSpan.Zero)
        {
            var minSec = (int)easyRange.MinPerKm.TotalSeconds;
            var maxSec = (int)easyRange.MaxPerKm.TotalSeconds;
            var minAllowed = (int)(minSec * 0.85);
            var maxAllowed = (int)(maxSec * 1.15);

            if (workout.TargetPaceEasySecPerKm < minAllowed || workout.TargetPaceEasySecPerKm > maxAllowed)
            {
                violations.Add($"Workout '{workout.Title}' easy pace {workout.TargetPaceEasySecPerKm}s/km outside range [{minAllowed}-{maxAllowed}]s/km.");
            }
        }

        if (workout.TargetPaceFastSecPerKm > 0 && paces.RepetitionPace.HasValue)
        {
            var repSec = (int)paces.RepetitionPace.Value.TotalSeconds;
            var absoluteMin = (int)(repSec * 0.90);

            if (workout.TargetPaceFastSecPerKm < absoluteMin)
            {
                violations.Add($"Workout '{workout.Title}' fast pace {workout.TargetPaceFastSecPerKm}s/km faster than rep floor ({absoluteMin}s/km).");
            }
        }
    }

    private static EvaluationResult BuildResult(List<string> violations)
    {
        var violationReason = violations.Count == 0
            ? "All plan constraints satisfied."
            : string.Join("\n", violations);
        var violationCount = new NumericMetric(ViolationsMetricName, value: violations.Count, reason: violationReason);

        var scoreReason = violations.Count == 0
            ? "Plan passes all constraint checks."
            : $"Plan has {violations.Count} violation(s).";
        var score = new NumericMetric(ScoreMetricName, value: violations.Count == 0 ? 1.0 : 0.0, reason: scoreReason);

        score.Interpretation = violations.Count == 0
            ? new EvaluationMetricInterpretation(EvaluationRating.Good, reason: "All constraints pass.")
            : new EvaluationMetricInterpretation(EvaluationRating.Unacceptable, failed: true, reason: scoreReason);

        return new EvaluationResult(violationCount, score);
    }
}
