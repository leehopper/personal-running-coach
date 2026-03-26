using System.Collections.Frozen;
using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Calculates VDOT from race times using the Daniels/Gilbert oxygen cost equations.
///
/// The formula uses two components:
/// 1. Oxygen cost (VO2) as a function of velocity: estimates the oxygen demand of running at a given speed.
/// 2. Fractional utilization (%VO2max) as a function of duration: estimates how much of VO2max a runner
///    can sustain for a given race duration.
///
/// VDOT = VO2 / %VO2max
///
/// Reference: Jack Daniels, "Daniels' Running Formula" (4th edition).
/// </summary>
public sealed class VdotCalculator : IVdotCalculator
{
    /// <summary>
    /// Standard race distances in meters, keyed by normalized distance name.
    /// </summary>
    private static readonly FrozenDictionary<string, double> DistanceMeters = new Dictionary<string, double>
    {
        ["5k"] = 5000.0,
        ["10k"] = 10000.0,
        ["half-marathon"] = 21097.5,
        ["halfmarathon"] = 21097.5,
        ["hm"] = 21097.5,
        ["marathon"] = 42195.0,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public decimal? CalculateVdot(RaceTime raceTime)
    {
        ArgumentNullException.ThrowIfNull(raceTime);

        if (!DistanceMeters.TryGetValue(raceTime.Distance, out var distanceInMeters))
        {
            return null;
        }

        var timeInMinutes = raceTime.Time.TotalMinutes;
        if (timeInMinutes <= 0)
        {
            return null;
        }

        var velocityMetersPerMinute = distanceInMeters / timeInMinutes;
        var oxygenCost = CalculateOxygenCost(velocityMetersPerMinute);
        var fractionalUtilization = CalculateFractionalUtilization(timeInMinutes);

        if (fractionalUtilization <= 0)
        {
            return null;
        }

        var vdot = oxygenCost / fractionalUtilization;

        return Math.Round((decimal)vdot, 1);
    }

    /// <inheritdoc />
    public decimal? CalculateVdot(IEnumerable<RaceTime> raceTimes)
    {
        ArgumentNullException.ThrowIfNull(raceTimes);

        var raceTimeList = raceTimes.ToList();
        if (raceTimeList.Count == 0)
        {
            return null;
        }

        // Calculate VDOT for each valid race time and return the highest value.
        // This selects the best performance, which is the most representative
        // of the runner's current fitness level.
        var vdotValues = raceTimeList
            .Select(CalculateVdot)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        return vdotValues.Count == 0 ? null : vdotValues.Max();
    }

    /// <summary>
    /// Calculates the oxygen cost of running at a given velocity.
    /// Formula: VO2 = -4.60 + 0.182258 * v + 0.000104 * v^2
    /// where v is velocity in meters per minute.
    /// </summary>
    private static double CalculateOxygenCost(double velocityMetersPerMinute)
    {
        var v = velocityMetersPerMinute;
        return -4.60 + (0.182258 * v) + (0.000104 * v * v);
    }

    /// <summary>
    /// Calculates the fractional utilization of VO2max for a given race duration.
    /// Formula: %VO2max = 0.8 + 0.1894393 * e^(-0.012778 * t) + 0.2989558 * e^(-0.1932605 * t)
    /// where t is time in minutes.
    /// </summary>
    private static double CalculateFractionalUtilization(double timeInMinutes)
    {
        return 0.8
            + (0.1894393 * Math.Exp(-0.012778 * timeInMinutes))
            + (0.2989558 * Math.Exp(-0.1932605 * timeInMinutes));
    }
}
