using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Coaching.Conversation;

/// <summary>
/// Compact, server-authoritative summary of the prescribed workout a conversational
/// log draft matched (Slice 4B PR4). Surfaced on the confirmation-card frame so the
/// runner sees "this matches your scheduled session" before confirming; <c>null</c> on
/// the card when the run matched no scheduled workout (an off-plan / unscheduled run),
/// in which case Confirm still commits an off-plan log. Resolved via the unchanged
/// <c>WorkoutLogService</c> prescription path, never from LLM extraction.
/// </summary>
/// <param name="WorkoutType">The prescribed workout type (e.g. Easy, Tempo, Long).</param>
/// <param name="DistanceMeters">The prescribed total distance in meters.</param>
/// <param name="DurationSeconds">The prescribed total duration in seconds.</param>
/// <param name="PaceFastSecPerKm">The fast (lower sec/km) bound of the prescribed pace band.</param>
/// <param name="PaceEasySecPerKm">The easy (higher sec/km) bound of the prescribed pace band.</param>
public sealed record CandidatePrescriptionDto(
    string WorkoutType,
    double DistanceMeters,
    double DurationSeconds,
    double PaceFastSecPerKm,
    double PaceEasySecPerKm)
{
    /// <summary>Projects a server-resolved <see cref="WorkoutPrescriptionSnapshot"/> onto the wire summary.</summary>
    /// <param name="snapshot">The resolved prescription snapshot, or <c>null</c> for an off-plan run.</param>
    /// <returns>The compact summary, or <c>null</c> when <paramref name="snapshot"/> is <c>null</c>.</returns>
    public static CandidatePrescriptionDto? FromSnapshot(WorkoutPrescriptionSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new CandidatePrescriptionDto(
                snapshot.WorkoutType.ToString(),
                snapshot.PrescribedDistance.Meters,
                snapshot.PrescribedDuration.TotalSeconds,
                snapshot.PrescribedPaceFast.SecondsPerKm,
                snapshot.PrescribedPaceSlow.SecondsPerKm);
}
