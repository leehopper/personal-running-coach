using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Computes Daniels–Gilbert pace zones for a given pace-zone index.
/// </summary>
public interface IPaceZoneCalculator
{
    /// <summary>
    /// Returns all training pace zones for the given <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Pace-zone index (25–90 inclusive).</param>
    /// <returns>All computed training pace zones.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside 25–90.</exception>
    TrainingPaces CalculatePaces(decimal index);
}
