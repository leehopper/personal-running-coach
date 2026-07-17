namespace RunCoach.Api.Modules.Training.Workouts;

/// <summary>
/// Compact, server-authoritative summary of the workout the active plan prescribes
/// for a given date (Slice 4 D1), surfaced on the log form so the runner sees what
/// was scheduled before recording what actually happened. <c>null</c> when the date
/// resolves to no prescription (off-plan, rest day, no active plan, or a malformed
/// stored prescription) — resolved via the unchanged
/// <see cref="IWorkoutLogService.ResolveCandidatePrescriptionAsync"/> path.
/// </summary>
/// <remarks>
/// This is a deliberate Training-module sibling of Coaching's
/// <c>CandidatePrescriptionDto</c> rather than a cross-module reuse of that type: the
/// two DTOs project the same <see cref="WorkoutPrescriptionSnapshot"/> shape for two
/// different surfaces (this one for the log form's prescribed banner, the other for
/// the conversational log-draft confirmation card), and each module owns its own wire
/// contract so a future change to one surface's shape cannot ripple into the other's
/// module. The one-record duplication is intentional, not an oversight.
/// </remarks>
/// <param name="WorkoutType">The prescribed workout type (e.g. Easy, Tempo, LongRun).</param>
/// <param name="DistanceMeters">The prescribed total distance in meters.</param>
/// <param name="DurationSeconds">The prescribed total duration in seconds.</param>
/// <param name="PaceFastSecPerKm">The fast (lower sec/km) bound of the prescribed pace band.</param>
/// <param name="PaceEasySecPerKm">The easy (higher sec/km) bound of the prescribed pace band.</param>
public sealed record PrescribedWorkoutDto(
    string WorkoutType,
    double DistanceMeters,
    double DurationSeconds,
    double PaceFastSecPerKm,
    double PaceEasySecPerKm)
{
    /// <summary>Projects a server-resolved <see cref="WorkoutPrescriptionSnapshot"/> onto the wire summary.</summary>
    /// <param name="snapshot">The resolved prescription snapshot, or <c>null</c> when there is none.</param>
    /// <returns>The compact summary, or <c>null</c> when <paramref name="snapshot"/> is <c>null</c>.</returns>
    public static PrescribedWorkoutDto? FromSnapshot(WorkoutPrescriptionSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new PrescribedWorkoutDto(
                snapshot.WorkoutType.ToString(),
                snapshot.PrescribedDistance.Meters,
                snapshot.PrescribedDuration.TotalSeconds,
                snapshot.PrescribedPaceFast.SecondsPerKm,
                snapshot.PrescribedPaceSlow.SecondsPerKm);
}
