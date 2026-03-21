using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Calculates VDOT (a measure of running fitness) from race times
/// using Daniels' Running Formula.
/// </summary>
public interface IVdotCalculator
{
    /// <summary>
    /// Calculates VDOT from a single race time.
    /// </summary>
    /// <param name="raceTime">A recorded race result.</param>
    /// <returns>The computed VDOT value, or null if the distance is not supported.</returns>
    decimal? CalculateVdot(RaceTime raceTime);

    /// <summary>
    /// Calculates VDOT from a collection of race times, selecting the best
    /// (highest VDOT) from the most recent results.
    /// </summary>
    /// <param name="raceTimes">A collection of recorded race results.</param>
    /// <returns>The computed VDOT value, or null if no valid race times are provided.</returns>
    decimal? CalculateVdot(IEnumerable<RaceTime> raceTimes);
}
