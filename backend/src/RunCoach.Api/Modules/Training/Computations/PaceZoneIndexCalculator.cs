using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Calculates a pace-zone index (Daniels-Gilbert fitness score) from race times
/// using the Daniels–Gilbert 1979 oxygen-cost equations via DanielsGilbertEquations.
///
/// Valid input range: race duration 3.5–300 minutes, velocity > 50 m/min.
/// Indices below 39 are outside the R-pace coefficient domain (R-035 caveat);
/// a Warning is logged but the value is still returned.
/// </summary>
public sealed partial class PaceZoneIndexCalculator(ILogger<PaceZoneIndexCalculator> logger)
    : IPaceZoneIndexCalculator
{
    private const double MinDurationMinutes = 3.5;
    private const double MaxDurationMinutes = 300.0;
    private const double MinVelocityMetersPerMinute = 50.0;
    private const int LowIndexThreshold = 39;

    /// <summary>
    /// Supported race distances in meters, keyed by normalized distance name.
    /// </summary>
    private static readonly FrozenDictionary<string, double> DistanceMeters =
        new Dictionary<string, double>
        {
            ["1500m"] = 1500.0,
            ["1 mile"] = 1609.344,
            ["1mile"] = 1609.344,
            ["mile"] = 1609.344,
            ["3k"] = 3000.0,
            ["2 mile"] = 3218.688,
            ["2mile"] = 3218.688,
            ["5k"] = 5000.0,
            ["10k"] = 10000.0,
            ["15k"] = 15000.0,
            ["half-marathon"] = 21097.5,
            ["halfmarathon"] = 21097.5,
            ["hm"] = 21097.5,
            ["marathon"] = 42195.0,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public decimal? CalculateIndex(RaceTime raceTime)
    {
        ArgumentNullException.ThrowIfNull(raceTime);

        if (!DistanceMeters.TryGetValue(raceTime.Distance, out var distanceInMeters))
        {
            return null;
        }

        var timeInMinutes = raceTime.Time.TotalMinutes;

        if (timeInMinutes < MinDurationMinutes || timeInMinutes > MaxDurationMinutes)
        {
            return null;
        }

        var velocityMetersPerMinute = distanceInMeters / timeInMinutes;

        if (velocityMetersPerMinute < MinVelocityMetersPerMinute)
        {
            return null;
        }

        var oxygenCost = DanielsGilbertEquations.OxygenCost(velocityMetersPerMinute);
        var fractionalUtilization = DanielsGilbertEquations.FractionalUtilization(timeInMinutes);

        if (fractionalUtilization <= 0)
        {
            return null;
        }

        var index = oxygenCost / fractionalUtilization;
        var rounded = Math.Round((decimal)index, 1);

        if (rounded < LowIndexThreshold)
        {
            LogLowIndex(logger, rounded, LowIndexThreshold);
        }

        return rounded;
    }

    /// <inheritdoc />
    public decimal? CalculateIndex(IEnumerable<RaceTime> raceTimes)
    {
        ArgumentNullException.ThrowIfNull(raceTimes);

        var raceTimeList = raceTimes.ToList();
        if (raceTimeList.Count == 0)
        {
            return null;
        }

        var indexValues = raceTimeList
            .Select(CalculateIndex)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        return indexValues.Count == 0 ? null : indexValues.Max();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Computed pace-zone index {Index} is below {Threshold} — outside the R-035 R-pace coefficient domain. Results may be less accurate.")]
    private static partial void LogLowIndex(ILogger logger, decimal index, int threshold);
}
