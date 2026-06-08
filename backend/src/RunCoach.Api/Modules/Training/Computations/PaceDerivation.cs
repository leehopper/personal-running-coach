using RunCoach.Api.Modules.Training.Models;

namespace RunCoach.Api.Modules.Training.Computations;

/// <summary>
/// Shared, divide-by-zero-guarded pace derivation (<see cref="Distance"/> /
/// <see cref="Duration"/> &#8594; seconds-per-km). Lifted out of the presentation-layer
/// <c>RecentLogFormatter</c> so the deviation engine and the prompt formatter share
/// one guarded derivation (Slice 3 PR2 / Unit 1).
/// </summary>
internal static class PaceDerivation
{
    /// <summary>
    /// Derives the average pace from distance and duration, or <c>null</c> when the
    /// inputs cannot yield a meaningful pace (zero/non-positive distance or duration —
    /// e.g. a skipped run). Never throws: the guard also protects
    /// <see cref="Pace.FromSecondsPerKm"/>, which rejects non-positive values.
    /// </summary>
    public static Pace? TryDerive(Distance distance, Duration duration)
    {
        if (distance.Kilometers <= 0 || duration.TotalSeconds <= 0)
        {
            return null;
        }

        return Pace.FromSecondsPerKm(duration.TotalSeconds / distance.Kilometers);
    }
}
