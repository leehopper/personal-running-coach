using RunCoach.Api.Modules.Training.Adaptation;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Projects a persisted <see cref="WorkoutLog"/> entity onto the
/// <see cref="LoggedWorkoutDetail"/> view the coaching prompt layer consumes (Slice 4B
/// conversation recent-logs feed). Tolerates an off-plan log (null
/// <see cref="WorkoutLog.Prescription"/>) by substituting a generic workout-type label —
/// unlike the adaptation handler's private mapper, which only ever sees on-plan logs.
/// Reuses <see cref="WorkoutMetricsProjection.ToDisplayMetrics"/> for the metrics map.
/// </summary>
public static class LoggedWorkoutDetailMapper
{
    /// <summary>
    /// The workout-type label used for an off-plan log with no prescribed type, per the
    /// <see cref="LoggedWorkoutDetail"/> contract ("a generic label such as Run").
    /// </summary>
    public const string OffPlanWorkoutType = "Run";

    /// <summary>Projects a <see cref="WorkoutLog"/> entity onto a <see cref="LoggedWorkoutDetail"/>.</summary>
    /// <param name="log">The persisted workout-log entity (its complex <c>Prescription</c> may be null).</param>
    /// <returns>The decoupled view, using the prescribed workout type when on-plan or <see cref="OffPlanWorkoutType"/> when off-plan.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="log"/> is null.</exception>
    public static LoggedWorkoutDetail ToLoggedWorkoutDetail(WorkoutLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var workoutType = log.Prescription?.WorkoutType.ToString() ?? OffPlanWorkoutType;

        return new LoggedWorkoutDetail(
            log.OccurredOn,
            workoutType,
            log.Distance,
            log.Duration,
            WorkoutMetricsProjection.ToDisplayMetrics(log.Metrics),
            log.Notes);
    }
}
