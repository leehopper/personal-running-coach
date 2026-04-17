using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Implements max-HR estimation via the Tanaka meta-analysis formula and
/// Daniels' %HRmax zone bands with optional Karvonen %HRR adjustment.
/// </summary>
public sealed class HeartRateZoneCalculator : IHeartRateZoneCalculator
{
    // Tanaka et al. (2001) meta-analysis of 18,712 subjects: HRmax = 208 - 0.7 * age
    private const double TanakaIntercept = 208.0;
    private const double TanakaSlope = 0.7;

    // Daniels' %HRmax zone percentages (lower, upper) per zone
    private const double EasyLowerPct = 0.65;
    private const double EasyUpperPct = 0.79;
    private const double MarathonLowerPct = 0.80;
    private const double MarathonUpperPct = 0.85;
    private const double ThresholdLowerPct = 0.88;
    private const double ThresholdUpperPct = 0.92;
    private const double IntervalLowerPct = 0.98;
    private const double IntervalUpperPct = 1.00;

    /// <inheritdoc />
    public int EstimateMaxHr(int age)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(age, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(age, 120);

        return (int)Math.Round(TanakaIntercept - (TanakaSlope * age));
    }

    /// <inheritdoc />
    public HeartRateZones CalculateZones(int maxHr, int? restingHr = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxHr, 1);

        if (restingHr is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(restingHr.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(restingHr.Value, maxHr);
        }

        return restingHr is null
            ? CalculateHrMaxZones(maxHr)
            : CalculateKarvonenZones(maxHr, restingHr.Value);
    }

    private static HeartRateZones CalculateHrMaxZones(int maxHr)
    {
        return new HeartRateZones(
            Easy: Band(maxHr, EasyLowerPct, EasyUpperPct),
            Marathon: Band(maxHr, MarathonLowerPct, MarathonUpperPct),
            Threshold: Band(maxHr, ThresholdLowerPct, ThresholdUpperPct),
            Interval: Band(maxHr, IntervalLowerPct, IntervalUpperPct),
            Repetition: null);
    }

    private static HeartRateZones CalculateKarvonenZones(int maxHr, int restingHr)
    {
        var hrr = maxHr - restingHr;
        return new HeartRateZones(
            Easy: KarvonenBand(restingHr, hrr, EasyLowerPct, EasyUpperPct),
            Marathon: KarvonenBand(restingHr, hrr, MarathonLowerPct, MarathonUpperPct),
            Threshold: KarvonenBand(restingHr, hrr, ThresholdLowerPct, ThresholdUpperPct),
            Interval: KarvonenBand(restingHr, hrr, IntervalLowerPct, IntervalUpperPct),
            Repetition: null);
    }

    private static IntRange Band(int maxHr, double lowerPct, double upperPct) =>
        new(
            lower: (int)Math.Round(maxHr * lowerPct),
            upper: (int)Math.Round(maxHr * upperPct));

    private static IntRange KarvonenBand(int restingHr, int hrr, double lowerPct, double upperPct) =>
        new(
            lower: (int)Math.Round(restingHr + (lowerPct * hrr)),
            upper: (int)Math.Round(restingHr + (upperPct * hrr)));
}
