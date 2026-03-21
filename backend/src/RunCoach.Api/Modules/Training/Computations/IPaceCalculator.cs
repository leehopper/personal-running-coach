using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Derives training pace zones from VDOT values using Daniels' Running Formula.
/// </summary>
public interface IPaceCalculator
{
    /// <summary>
    /// Calculates training pace zones for the given VDOT value.
    /// Returns easy pace as a range (min/max per km) and single-point paces
    /// for marathon, threshold, interval, and repetition zones.
    /// </summary>
    /// <param name="vdot">The runner's VDOT fitness value (typically 30-85).</param>
    /// <returns>Training paces derived from the VDOT value.</returns>
    TrainingPaces CalculatePaces(decimal vdot);

    /// <summary>
    /// Estimates maximum heart rate using the 220-age formula as a fallback
    /// when no measured max HR is available.
    /// </summary>
    /// <param name="age">The runner's age in years.</param>
    /// <returns>Estimated maximum heart rate in beats per minute.</returns>
    int EstimateMaxHr(int age);
}
