using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Computes Daniels–Gilbert pace zones on demand from the 1979 equations.
/// No lookup table — every zone is derived from <see cref="DanielsGilbertEquations"/> at call time.
/// </summary>
public sealed class PaceZoneCalculator : IPaceZoneCalculator
{
    private const double MinIndex = 25.0;
    private const double MaxIndex = 90.0;

    private const double EasyFastFraction = 0.70;
    private const double EasySlowFraction = 0.59;
    private const double ThresholdFraction = 0.880;
    private const double IntervalFraction = 0.973;

    private const double MarathonDistanceMeters = 42195.0;
    private const double Rep3kDistanceMeters = 3000.0;
    private const double Rep800DistanceMeters = 800.0;

    /// <summary>
    /// R-400 multiplier: 400m repetition fraction of 3000m race pace (R-028).
    /// Derived as 0.9450 × (400/3000) of t3k.
    /// </summary>
    private static readonly double R400Multiplier = 0.9450 * (400.0 / 3000.0);

    /// <inheritdoc />
    public TrainingPaces CalculatePaces(decimal index)
    {
        var idx = (double)index;

        if (idx < MinIndex || idx > MaxIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Pace-zone index must be between {MinIndex} and {MaxIndex} (inclusive). Got: {index}.");
        }

        var easyFastVelocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(EasyFastFraction * idx);
        var easySlowVelocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(EasySlowFraction * idx);
        var easyRange = new PaceRange(
            fast: VelocityToPace(easyFastVelocity),
            slow: VelocityToPace(easySlowVelocity));

        var marathonTimeMinutes = DanielsGilbertEquations.PredictRaceTimeMinutes(idx, MarathonDistanceMeters);
        var marathonPace = RaceTimeToPace(marathonTimeMinutes, MarathonDistanceMeters);

        var thresholdVelocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(ThresholdFraction * idx);
        var thresholdPace = VelocityToPace(thresholdVelocity);

        var intervalVelocity = DanielsGilbertEquations.SolveVelocityForTargetVo2(IntervalFraction * idx);
        var intervalPace = VelocityToPace(intervalVelocity);

        // R zone: derive rep intervals from predicted 3000m race time (R-028, R-035).
        var t3k = DanielsGilbertEquations.PredictRaceTimeMinutes(idx, Rep3kDistanceMeters);
        var r400Minutes = R400Multiplier * t3k;
        var r400Pace = RaceTimeToPace(r400Minutes, 400.0);

        // F zone: derive fast-rep intervals from predicted 800m race time (R-035).
        var t800 = DanielsGilbertEquations.PredictRaceTimeMinutes(idx, Rep800DistanceMeters);
        var f400Pace = RaceTimeToPace(t800 / 2.0, 400.0);

        return new TrainingPaces(
            EasyPaceRange: easyRange,
            MarathonPace: marathonPace,
            ThresholdPace: thresholdPace,
            IntervalPace: intervalPace,
            RepetitionPace: r400Pace,
            FastRepetitionPace: f400Pace);
    }

    private static Pace VelocityToPace(double velocityMetersPerMinute)
    {
        // velocity (m/min) → pace (s/km): 60_000 / velocity
        var secondsPerKm = 60_000.0 / velocityMetersPerMinute;
        return Pace.FromSecondsPerKm(secondsPerKm);
    }

    private static Pace RaceTimeToPace(double timeMinutes, double distanceMeters)
    {
        var distanceKm = distanceMeters / 1000.0;
        var secondsPerKm = (timeMinutes * 60.0) / distanceKm;
        return Pace.FromSecondsPerKm(secondsPerKm);
    }
}
