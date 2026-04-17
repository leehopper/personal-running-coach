using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Calculates a pace-zone index (Daniels-Gilbert fitness score) from race times
/// using the Daniels–Gilbert 1979 oxygen-cost equations.
/// </summary>
public interface IPaceZoneIndexCalculator
{
    /// <summary>
    /// Calculates the pace-zone index from a single race time.
    /// Returns null when the distance is unsupported, the duration is outside
    /// the valid range (3.5–300 min), or the computed velocity is below 50 m/min.
    /// </summary>
    /// <param name="raceTime">A recorded race result.</param>
    /// <returns>The computed index (rounded to 1 decimal), or null.</returns>
    decimal? CalculateIndex(RaceTime raceTime);

    /// <summary>
    /// Calculates the pace-zone index from a collection of race times, selecting
    /// the best (highest index) from valid results.
    /// </summary>
    /// <param name="raceTimes">A collection of recorded race results.</param>
    /// <returns>The highest computed index, or null if no valid times exist.</returns>
    decimal? CalculateIndex(IEnumerable<RaceTime> raceTimes);
}
