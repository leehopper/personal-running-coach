using RunCoach.Api.Modules.Training.Computations;
using RunCoach.Api.Modules.Training.Workouts;

namespace RunCoach.Api.Modules.Training.Adaptation;

/// <summary>
/// Deterministic deviation engine (Slice 3 PR2 / Unit 1): compares a logged
/// workout's actuals against its frozen <see cref="WorkoutPrescriptionSnapshot"/>
/// and reports band-membership pace deviation plus signed distance/duration
/// percentages. Pure and stateless — owns no thresholds or escalation policy.
/// </summary>
public sealed class DeviationEngine : IDeviationEngine
{
    /// <inheritdoc />
    public DeviationResult? Evaluate(WorkoutLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        // Off-plan: a null prescription snapshot is a no-op for adaptation.
        if (log.Prescription is not { } snapshot)
        {
            return null;
        }

        var (paceBand, paceDeviation) = ClassifyPace(log, snapshot);

        return new DeviationResult(
            log.OccurredOn,
            log.CompletionStatus,
            WorkoutKind.IsKey(snapshot.WorkoutType),
            SignedPercent(log.Distance.Kilometers, snapshot.PrescribedDistance.Kilometers),
            SignedPercent(log.Duration.TotalSeconds, snapshot.PrescribedDuration.TotalSeconds),
            paceBand,
            paceDeviation);
    }

    /// <summary>
    /// Signed percentage of actual-vs-prescribed: <c>(actual - prescribed) / prescribed * 100</c>.
    /// Guards a non-positive prescribed value (degenerate snapshot) by reporting no deviation.
    /// </summary>
    private static double SignedPercent(double actual, double prescribed) =>
        prescribed <= 0 ? 0.0 : (actual - prescribed) / prescribed * 100.0;

    /// <summary>
    /// Classifies the derived pace as band membership relative to the snapshot's
    /// Fast/Slow band. A skipped run, or a log whose distance/duration cannot yield a
    /// pace, reports <see cref="PaceBandMembership.Unknown"/> with a zero magnitude so
    /// no spurious pace deviation is produced.
    /// </summary>
    private static (PaceBandMembership Band, double DeviationSecondsPerKm) ClassifyPace(
        WorkoutLog log,
        WorkoutPrescriptionSnapshot snapshot)
    {
        if (log.CompletionStatus == CompletionStatus.Skipped ||
            PaceDerivation.TryDerive(log.Distance, log.Duration) is not { } actualPace)
        {
            return (PaceBandMembership.Unknown, 0.0);
        }

        var band = snapshot.PrescribedPace;
        if (actualPace.IsFasterThan(band.Fast))
        {
            // Negative magnitude: faster than the Fast bound (lower sec/km).
            return (PaceBandMembership.FasterThanFast, actualPace.SecondsPerKm - band.Fast.SecondsPerKm);
        }

        if (actualPace.IsSlowerThan(band.Slow))
        {
            // Positive magnitude: slower than the Slow bound (higher sec/km).
            return (PaceBandMembership.SlowerThanSlow, actualPace.SecondsPerKm - band.Slow.SecondsPerKm);
        }

        return (PaceBandMembership.InsideBand, 0.0);
    }
}
