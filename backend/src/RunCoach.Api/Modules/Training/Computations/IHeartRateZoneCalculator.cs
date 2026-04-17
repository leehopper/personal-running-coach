using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Estimates maximum heart rate and derives Daniels %HRmax zone bands
/// with optional Karvonen heart-rate reserve adjustment.
/// </summary>
public interface IHeartRateZoneCalculator
{
    /// <summary>
    /// Estimates maximum heart rate using the Tanaka meta-analysis formula
    /// 208 − 0.7 · age (R-031, 18,712-subject validation).
    /// </summary>
    /// <param name="age">The runner's age in years (1–120).</param>
    /// <returns>Estimated maximum heart rate in beats per minute.</returns>
    int EstimateMaxHr(int age);

    /// <summary>
    /// Computes Daniels %HRmax zone bands from the given max heart rate.
    /// When <paramref name="restingHr"/> is provided, uses the Karvonen %HRR
    /// formula instead of raw %HRmax.
    /// </summary>
    /// <param name="maxHr">Maximum heart rate in bpm.</param>
    /// <param name="restingHr">Resting heart rate in bpm, or null for %HRmax mode.</param>
    /// <returns>Zone bands in bpm, with Repetition always null.</returns>
    HeartRateZones CalculateZones(int maxHr, int? restingHr = null);
}
