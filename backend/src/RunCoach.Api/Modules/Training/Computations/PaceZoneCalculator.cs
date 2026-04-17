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
    private const double LowIndexWarningThreshold = 30.0;

    private const double EasyFastFraction = 0.70;
    private const double EasySlowFraction = 0.59;
    private const double ThresholdFraction = 0.880;
    private const double IntervalFraction = 0.973;

    private const double MarathonDistanceMeters = 42195.0;

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

        if (idx < LowIndexWarningThreshold)
        {
            // Index 25–29: below the validated domain floor for R-pace coefficients (R-035)
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

        return new TrainingPaces(
            EasyPaceRange: easyRange,
            MarathonPace: marathonPace,
            ThresholdPace: thresholdPace,
            IntervalPace: intervalPace,
            RepetitionPace: null,
            FastRepetitionPace: null);
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
